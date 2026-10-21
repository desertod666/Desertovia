using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Director;
using Desertod.Domain.Dungeons;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;
[Collection(ApiCollection.Name)]
public sealed class AlmightyApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    [Fact]
    public async Task Stale_vigor_proposal_does_not_apply_to_changed_settings()
    {
        var v=await players.FoundVillageAsync("Stale tonic");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        var service=factory.Services.GetRequiredService<GameService>();var review=(await service.BeginAlmightyReviewAsync(id,default))!;
        await service.CompleteAlmightyReviewAsync(review,new("vigor_frequent","Я вижу усталость.","Проверить взвар."),default);
        var p=await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.VillageId==id);
        var village=await db.Villages.SingleAsync(x=>x.Id==id);village.VigorCooldownMinutes=180;await db.SaveChangesAsync();
        Assert.True((await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{p.Id}/vote",new{accept=true})).IsSuccessStatusCode);
        Assert.Equal("Superseded",(await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.Id==p.Id)).Status);
        Assert.Equal(180,(await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id)).VigorCooldownMinutes);
    }

    [Fact]
    public async Task Vigor_tuning_needs_owner_approval_is_persisted_and_repeat_vote_does_not_tune_twice()
    {
        var v=await players.FoundVillageAsync("Tonic tuning");var member=await players.JoinAsync(v,"Viewer");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        var service=factory.Services.GetRequiredService<GameService>();
        var review=(await service.BeginAlmightyReviewAsync(id,default))!;
        Assert.Contains(review.Options,o=>o.Id=="vigor_stronger");
        Assert.True(await service.CompleteAlmightyReviewAsync(review,new("vigor_stronger","Я вижу нехватку сил.","Сохранить медленный труд."),default));
        var proposal=await db.AlmightyProposals.AsNoTracking().SingleAsync(p=>p.VillageId==id);
        Assert.Equal(8,(await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id)).VigorEnergy);
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Client.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true})).StatusCode);
        for(var i=0;i<2;i++)Assert.True((await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true})).IsSuccessStatusCode);
        Assert.Equal(10,(await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id)).VigorEnergy);
        var me=await member.Client.GetFromJsonAsync<JsonElement>(v.Path+"/me");Assert.Equal(10,me.GetProperty("vigorEnergy").GetInt32());
    }

    [Fact]
    public async Task Failed_model_response_records_specific_reason_without_creating_a_proposal()
    {
        var v=await players.FoundVillageAsync("Director failure");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        var service=factory.Services.GetRequiredService<GameService>();var review=(await service.BeginAlmightyReviewAsync(id,default))!;
        await service.CompleteAlmightyReviewAsync(review,new("none","","","model.json"),default);
        Assert.Empty(await db.AlmightyProposals.Where(x=>x.VillageId==id).ToArrayAsync());
        var observation=await db.AlmightyObservations.SingleAsync(x=>x.VillageId==id);
        Assert.Contains("повреждённый формат",observation.Reason);
    }

    [Fact]
    public async Task Only_owner_reads_modes_and_citizens_cannot_forge_signed_callbacks()
    {
        var v=await players.FoundVillageAsync("Almighty access");
        var member=await players.JoinAsync(v,"Viewer");
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Client.GetAsync(v.Path+"/director")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Client.PutAsJsonAsync(v.Path+"/director/mode",new{mode="Off"})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await member.Client.PostAsJsonAsync("/api/director/vote",new{proposalId=Guid.NewGuid(),ownerId="1",accept=true})).StatusCode);
        var view=await v.Tsar.GetFromJsonAsync<AlmightyView>(v.Path+"/director");
        Assert.NotNull(view); Assert.Equal("Propose",view.State.Mode);
    }
    [Fact]
    public async Task Lease_is_exclusive_reject_is_persistent_and_approval_rechecks_current_conditions()
    {
        var v=await players.FoundVillageAsync("Almighty review");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        var service=factory.Services.GetRequiredService<GameService>();
        var claims=await Task.WhenAll(service.BeginAlmightyReviewAsync(id,default),service.BeginAlmightyReviewAsync(id,default));
        var review=Assert.Single(claims, r=>r is not null)!;
        Assert.True(review.Snapshot.Length < 36000);
        Assert.NotEmpty(JsonDocument.Parse(review.Snapshot).RootElement.GetProperty("catalog").GetProperty("items").EnumerateArray());
        Assert.True(await service.CompleteAlmightyReviewAsync(review,new("economy_report","Я вижу запасы и предлагаю их оценить.","Сохранять медленную игру."),default));
        Assert.False(await service.CompleteAlmightyReviewAsync(review,new("zombies_small","Повтор","Нельзя"),default));
        var p=await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.VillageId==id);
        var replies=await Task.WhenAll(v.Tsar.PostAsJsonAsync(v.Path+$"/director/{p.Id}/vote",new{accept=true}),v.Tsar.PostAsJsonAsync(v.Path+$"/director/{p.Id}/vote",new{accept=true}));
        Assert.All(replies,r=>Assert.True(r.IsSuccessStatusCode));
        Assert.Equal("Applied",(await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.Id==p.Id)).Status);
        Assert.Empty((await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id)).Raids);
        Assert.Single(await db.AlmightyObservations.Where(x=>x.VillageId==id).ToListAsync());
    }
    [Fact]
    public async Task Off_fences_a_late_model_result_and_retains_existing_game_progress()
    {
        var v=await players.FoundVillageAsync("Almighty stop");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        var service=factory.Services.GetRequiredService<GameService>();
        var review=await service.BeginAlmightyReviewAsync(village.Id,default);
        Assert.NotNull(review);
        Assert.True((await v.Tsar.PutAsJsonAsync(v.Path+"/director/mode",new{mode="Off"})).IsSuccessStatusCode);
        Assert.False(await service.CompleteAlmightyReviewAsync(review,new("economy_report","Поздно","Поздно"),default));
        Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==village.Id).ToListAsync());
        Assert.Null(await service.BeginAlmightyReviewAsync(village.Id,default));
    }
    [Fact]
    public async Task Fresh_rest_conditions_are_rechecked_and_duplicate_approval_does_not_extend_the_timer()
    {
        var v=await players.FoundVillageAsync("Director recovery");
        for (var i = 0; i < 3; i++) {
            var resident=await players.JoinAsync(v,"Patient"+i);
            await players.ArrangeCitizenAsync(resident.Id,(c,now)=>c.InjuredUntil=now.AddHours(1));
            factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id,v.Slug);
        }
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        var game=factory.Services.GetRequiredService<GameService>();
        var review=await game.BeginAlmightyReviewAsync(id,default);
        Assert.NotNull(review);Assert.Contains(review.Options,o=>o.Id=="rest");
        await game.CompleteAlmightyReviewAsync(review,new("rest","Я вижу раненого. Предлагаю передышку.","Восстановление"),default);
        var proposal=await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.VillageId==id);
        await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true});
        var until=(await db.AlmightyStates.AsNoTracking().SingleAsync(x=>x.VillageId==id)).RestUntil;
        Assert.Equal((await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.Id==proposal.Id)).DecidedAt!.Value.AddHours(2),until);
        await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true});
        Assert.Equal(until,(await db.AlmightyStates.AsNoTracking().SingleAsync(x=>x.VillageId==id)).RestUntil);
    }
    [Fact]
    public async Task Unknown_model_tool_has_no_effect_or_proposal()
    {
        var v=await players.FoundVillageAsync("Director invalid");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        var game=factory.Services.GetRequiredService<GameService>();
        var review=await game.BeginAlmightyReviewAsync(id,default);
        Assert.NotNull(review);
        await game.CompleteAlmightyReviewAsync(review,new("give_real_candles","Выдать деньги","Игнорировать правила"),default);
        Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==id).ToListAsync());
        Assert.NotEmpty((await db.AlmightyStates.AsNoTracking().SingleAsync(p=>p.VillageId==id)).LastError);
    }

    [Fact]
    public async Task Signed_bot_identity_must_match_the_linked_owner_and_repeat_is_idempotent()
    {
        var v=await players.FoundVillageAsync("Director Telegram");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        var uid="809"+System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000000000).ToString();
        db.ExternalLogins.Add(new Desertod.Domain.Entities.ExternalLogin { Provider="telegram",ProviderUserId=uid,AccountId=village.OwnerAccountId });
        await db.SaveChangesAsync();
        var game=factory.Services.GetRequiredService<GameService>();
        var review=await game.BeginAlmightyReviewAsync(village.Id,default);
        Assert.NotNull(review);
        await game.CompleteAlmightyReviewAsync(review,new("economy_report","Я вижу запасы.","Наблюдать"),default);
        var proposal=await db.AlmightyProposals.AsNoTracking().SingleAsync(p=>p.VillageId==village.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>game.DecideAlmightyAsync(proposal.Id,null,"forged",true,default));
        var result=await game.DecideAlmightyAsync(proposal.Id,null,uid,true,default);
        Assert.Equal(result,await game.DecideAlmightyAsync(proposal.Id,null,uid,true,default));
        var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var options=Microsoft.Extensions.Options.Options.Create(new Desertod.Api.Auth.TelegramOptions {BotToken=DesertodApiFactory.TelegramBotToken,BotUsername="test_bot"});
        Assert.False(AlmightyService.Trusted(http,options));
        http.Request.Headers["X-Desertovia-Bridge"]=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("desertovia-bridge:"+DesertodApiFactory.TelegramBotToken)));
        Assert.True(AlmightyService.Trusted(http,options));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Large_village_review_fits_the_bridge_limit_with_history_and_direction_options(bool delegatedFinance)
    {
        var v=await players.FoundVillageAsync("Large director snapshot");
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var now=factory.Clock.GetUtcNow();
        city.CreatedAt=now.AddDays(-10);city.RaidsPlannedPeriod=long.MaxValue;
        for(var i=0;i<150;i++)
        {
            var account=new Desertod.Domain.Entities.Account{DisplayName="Synthetic "+i,CreatedAt=now};db.Accounts.Add(account);
            var c=Desertod.Domain.Rules.CharacterCreation.Create(city.Id,account.Id,"Synthetic "+i,now);
            c.DefenseSector="north";
            if(i==0){c.CurrentTaskId=Desertod.Domain.Catalog.GameCatalog.KeepWatch;c.CycleStartedAt=now;c.CycleEndsAt=now.AddHours(1);c.CycleSettled=false;}
            db.Citizens.Add(c);factory.Services.GetRequiredService<CitizenPresence>().Touch(c.Id,v.Slug);
        }
        db.AlmightyStates.Add(new(){VillageId=city.Id,Mode=delegatedFinance?"AutoEconomy":"Propose",Intent=new string('я',1000),NextReviewAt=now});
        if(delegatedFinance)db.DirectorFinancePolicies.Add(new(){VillageId=city.Id,Version=1,RequestId=Guid.NewGuid(),OwnerAccountId=city.OwnerAccountId,At=now,
            Limits=new(){Enabled=true,ActionsPerDay=5,TaxChanges=true,MaximumTaxBasisPoints=2000,TaxStepBasisPoints=100,TaxNoticeMinutes=60,TaxCooldownMinutes=120}});
        for(var i=0;i<4;i++)db.AlmightyProposals.Add(new(){VillageId=city.Id,ReviewId=Guid.CreateVersion7(),Reason=new string('я',1200),Result=new string('я',500),Status="Applied",CreatedAt=now.AddDays(-1).AddSeconds(i)});
        for(var i=0;i<20;i++)db.ChatMessages.Add(Desertod.Domain.Chat.ChatMessage.Write(city.Id,city.OwnerAccountId,null,"Царь",new string('я',120),now.AddSeconds(-20+i)));
        // Synthetic maximum returned funding schedule must fit alongside all other observed facts.
        for(var i=0;i<30;i++)db.TreasuryFundingPolicies.Add(new(){VillageId=city.Id,Version=i+1,
            RequestId=Guid.NewGuid(),ActorId=city.OwnerAccountId,Enabled=true,DailyIssueLimit=(i+1)*1000,
            PublishedAt=now,EffectiveAt=now.AddMinutes(i+1)});
        await db.SaveChangesAsync();
        var review=await factory.Services.GetRequiredService<GameService>().BeginAlmightyReviewAsync(city.Id,default);
        Assert.NotNull(review);Assert.Contains(review.Options,o=>o.Composition=="elite");Assert.Contains(review.Options,o=>o.Composition=="boss");
        Assert.InRange(review.Snapshot.Length,1,36000);
        using var fundingSnapshot=JsonDocument.Parse(review.Snapshot);
        var funding=fundingSnapshot.RootElement.GetProperty("economy").GetProperty("analysis").GetProperty("funds").GetProperty("treasuryFunding");
        Assert.Equal(30,funding.GetProperty("scheduled").GetProperty("rows").GetArrayLength());
        Assert.Equal(0,funding.GetProperty("issuedToday").GetInt64());
        if(delegatedFinance)Assert.Equal(1,fundingSnapshot.RootElement.GetProperty("finance").GetProperty("policyVersion").GetInt64());
    }

    [Fact]
    public async Task Overdue_raid_gets_an_explicit_pacing_proposal_even_if_model_keeps_waiting_for_repairs()
    {
        var v = await players.FoundVillageAsync("Director pressure");
        for (var i = 0; i < 4; i++) {
            var resident = await players.JoinAsync(v, "Ready"+i);
            factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id, v.Slug);
        }
        await using var db = await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var city = await db.Villages.SingleAsync(x => x.Slug == v.Slug);
        city.CreatedAt = factory.Clock.GetUtcNow().AddDays(-10);
        city.RaidsPlannedPeriod = long.MaxValue;
        await db.SaveChangesAsync();
        var game = factory.Services.GetRequiredService<GameService>();
        var review = (await game.BeginAlmightyReviewAsync(city.Id, default))!;
        Assert.True(JsonDocument.Parse(review.Snapshot).RootElement.GetProperty("raidPressure").GetProperty("proposalDue").GetBoolean());
        await game.CompleteAlmightyReviewAsync(review, new("none", "Ждём ремонта и окончания стройки.", "Ждать всех."), default);
        var proposal = await db.AlmightyProposals.AsNoTracking().SingleAsync(x => x.VillageId == city.Id);
        Assert.Contains(review.Options, o => o.Id == proposal.Option && o.Enemy is not null);
        Assert.Contains("Планировщик угроз", proposal.Reason);
        Assert.Empty((await db.Villages.AsNoTracking().SingleAsync(x => x.Id == city.Id)).Raids);
        await game.DecideAlmightyAsync(proposal.Id, city.OwnerAccountId, null, false, default);
        // Отказ не обходится новым врагом и новая модель не должна навязать ещё одну заявку.
        var state = await db.AlmightyStates.SingleAsync(x => x.VillageId == city.Id);
        state.NextReviewAt = factory.Clock.GetUtcNow(); state.LastNoticeAt = null;
        await db.SaveChangesAsync();
        var second = (await game.BeginAlmightyReviewAsync(city.Id, default))!;
        Assert.False(JsonDocument.Parse(second.Snapshot).RootElement.GetProperty("raidPressure").GetProperty("proposalDue").GetBoolean());
        var otherRaid = second.Options.First(o => o.Enemy is not null && o.Id != proposal.Option);
        await game.CompleteAlmightyReviewAsync(second, new(otherRaid.Id, "Другой враг.", "Атака."), default);
        Assert.Single(await db.AlmightyProposals.AsNoTracking().Where(x => x.VillageId == city.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Invalid_model_answer_does_not_start_the_pacing_fallback_or_create_a_raid()
    {
        var v = await players.FoundVillageAsync("Pressure model failure");
        for (var i = 0; i < 4; i++) {
            var resident = await players.JoinAsync(v, "Ready"+i);
            factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id, v.Slug);
        }
        await using var db = await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var city = await db.Villages.SingleAsync(x => x.Slug == v.Slug);
        city.CreatedAt = factory.Clock.GetUtcNow().AddDays(-10); city.RaidsPlannedPeriod = long.MaxValue;
        await db.SaveChangesAsync();
        var game = factory.Services.GetRequiredService<GameService>();
        var review = (await game.BeginAlmightyReviewAsync(city.Id, default))!;
        await game.CompleteAlmightyReviewAsync(review, new("none", "", "", "budget.shared"), default);
        Assert.Empty(await db.AlmightyProposals.Where(x => x.VillageId == city.Id).ToArrayAsync());
        Assert.Empty((await db.Villages.AsNoTracking().SingleAsync(x => x.Id == city.Id)).Raids);
    }

    [Fact]
    public async Task Raid_proposal_rechecks_ready_online_residents_on_owner_approval()
    {
        var v = await players.FoundVillageAsync("Pressure approval recheck");
        var residents = new List<Guid>();
        for (var i = 0; i < 4; i++) {
            var resident = await players.JoinAsync(v, "Ready"+i); residents.Add(resident.Id);
            factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id, v.Slug);
        }
        await using var db = await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var city = await db.Villages.SingleAsync(x => x.Slug == v.Slug);
        city.CreatedAt = factory.Clock.GetUtcNow().AddDays(-10); city.RaidsPlannedPeriod = long.MaxValue;
        await db.SaveChangesAsync();
        var game = factory.Services.GetRequiredService<GameService>();
        var review = (await game.BeginAlmightyReviewAsync(city.Id, default))!;
        await game.CompleteAlmightyReviewAsync(review, new("none", "Ничего не делать.", "Ждать."), default);
        var proposal = await db.AlmightyProposals.AsNoTracking().SingleAsync(x => x.VillageId == city.Id);
        foreach (var id in residents) await players.ArrangeCitizenAsync(id,(c,now)=>c.InjuredUntil=now.AddHours(1));
        await game.DecideAlmightyAsync(proposal.Id, city.OwnerAccountId, null, true, default);
        Assert.Equal("Superseded", (await db.AlmightyProposals.AsNoTracking().SingleAsync(x => x.Id == proposal.Id)).Status);
        Assert.Empty((await db.Villages.AsNoTracking().SingleAsync(x => x.Id == city.Id)).Raids);
    }

    [Fact]
    public async Task Dungeon_proposal_rechecks_the_owners_personal_fight_and_public_open_has_the_same_admission()
    {
        var v=await players.FoundVillageAsync("Busy royal hunter");
        for(var i=0;i<3;i++)
        {
            var resident=await players.JoinAsync(v,"Ready"+i);
            factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id,v.Slug);
        }
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        var game=factory.Services.GetRequiredService<GameService>();
        var review=(await game.BeginAlmightyReviewAsync(city.Id,default))!;
        Assert.Contains(review.Options,o=>o.Id=="dungeon");
        Assert.True(await game.CompleteAlmightyReviewAsync(review,new("dungeon","Общий поход.","Подготовить сбор."),default));
        var proposal=await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.VillageId==city.Id);
        // Another accepted activity can start after the proposal. Only the private admission fact is arranged.
        var personal=new DungeonRun{VillageId=city.Id,Kind=DungeonRunKind.Ambush,DungeonId="ambush-wolf",Phase=DungeonPhase.Running,
            OpenedAt=factory.Clock.GetUtcNow(),Heroes=[new(){AccountId=city.OwnerAccountId,Name="Tsar",Class=HeroClass.Brawler}]};
        db.DungeonRuns.Add(personal);await db.SaveChangesAsync();
        try
        {
            await Players.AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}),"dungeon.busy");
            await game.DecideAlmightyAsync(proposal.Id,city.OwnerAccountId,null,true,default);
            Assert.Equal("Superseded",(await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.Id==proposal.Id)).Status);
            Assert.False(await db.DungeonRuns.AnyAsync(r=>r.VillageId==city.Id&&r.Kind==DungeonRunKind.Group));
            await game.SetAlmightyModeAsync(v.Slug,city.OwnerAccountId,"Propose",default);
            var fresh=(await game.BeginAlmightyReviewAsync(city.Id,default))!;
            Assert.DoesNotContain(fresh.Options,o=>o.Id=="dungeon");
        }
        finally{db.DungeonRuns.Remove(personal);await db.SaveChangesAsync();}
    }

}
