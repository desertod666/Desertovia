using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CombatActorApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task Cleanup(FoundedVillage v)
    {
        await using var db=await Db();
        await db.Villages.Where(w=>w.Slug==v.Slug).ExecuteDeleteAsync();
    }
    private async Task<Guid> Battle(FoundedVillage v,Guid? citizen=null,bool royal=false)
    {
        await using var db=await Db();var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var now=factory.Clock.GetUtcNow();
        var b=new RaidBattle{Id=Guid.CreateVersion7(),VillageId=w.Id,Sector="west",Phase="Fighting",
            StartsAt=now.AddHours(1),LastTickAt=now.AddHours(1),EndsAt=now.AddHours(2),
            Units=[new(){X=18.5,Y=13.5,SpawnX=18.5,SpawnY=13.5,Health=100,MaximumHealth=100,Attack=1,NextAttackAt=now.AddHours(1)}]};
        if(royal||citizen is not null)b.Fighters.Add(new(){IsTsar=royal,AccountId=royal?w.OwnerAccountId:null,
            CitizenId=citizen??Guid.Empty,Active=true,Health=7,MaximumHealth=32,X=15.5,Y=13.5,NextAttackAt=now.AddHours(1)});
        db.RaidBattles.Add(b);await db.SaveChangesAsync();return b.Id;
    }

    [Fact]
    public async Task A_raid_fighter_cannot_join_a_dungeon_or_discard_equipment_and_a_fighting_ruler_cannot_open_one()
    {
        var v=await players.FoundVillageAsync("Single battle");try
        {
            var c=await players.JoinAsync(v);await Battle(v,c.Id);
            await players.ArrangeVillageAsync(v.Slug,(w,now)=>{w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));w.Store(ResourceKind.Spear,1);});
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}));
            var before=await players.LoadCitizenAsync(c.Id);
            await AssertRuleBrokenAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/dungeon",new{@class="Spearman"}),"goto.retreatRequired");
            await AssertRuleBrokenAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood",requestId=Guid.NewGuid()}),"goto.retreatRequired");
            var after=await players.LoadCitizenAsync(c.Id);
            Assert.Null(after.DungeonRunId);Assert.Equal(before.Weapon,after.Weapon);Assert.Equal(before.StoredEnergy,after.StoredEnergy);
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);Assert.Equal(1,w.Stock(ResourceKind.Spear));
                Assert.Single((await db.DungeonRuns.SingleAsync(r=>r.VillageId==w.Id)).Heroes);
            }
            await OkJsonAsync(await v.Tsar.DeleteAsync(v.Path+"/dungeon"));await Battle(v,royal:true);
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}),"goto.retreatRequired");
            await using var read=await Db();Assert.Equal(1,await read.DungeonRuns.CountAsync(r=>r.VillageId==after.VillageId));
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task A_retry_of_work_accepted_before_combat_keeps_the_cycle_and_does_not_charge_again()
    {
        var v=await players.FoundVillageAsync("Work receipt during combat");try
        {
            var c=await players.JoinAsync(v);var request=new{taskId="chop-wood",requestId=Guid.NewGuid()};
            await OkJsonAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/task",request));
            var before=await players.LoadCitizenAsync(c.Id);await Battle(v,c.Id);
            await OkJsonAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/task",request));
            await AssertRuleBrokenAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood",requestId=Guid.NewGuid()}),"goto.retreatRequired");
            var after=await players.LoadCitizenAsync(c.Id);
            Assert.Equal(before.CycleStartedAt,after.CycleStartedAt);Assert.Equal(before.CycleEndsAt,after.CycleEndsAt);
            Assert.Equal(before.StoredEnergy,after.StoredEnergy);Assert.Equal(before.CycleEncounterSeed,after.CycleEncounterSeed);
            await using var db=await Db();Assert.Equal(1,await db.EconomyReceipts.CountAsync(r=>r.VillageId==after.VillageId&&r.RequestId==request.requestId));
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task A_ruler_with_a_legacy_resident_fighter_cannot_swap_bodies_heal_by_order_or_escape_into_rest_or_travel()
    {
        var v=await players.FoundVillageAsync("One ruler body");try
        {
            Guid citizen;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);
                var c=CharacterCreation.Create(w.Id,w.OwnerAccountId,"Resident ruler",factory.Clock.GetUtcNow());
                db.Citizens.Add(c);await db.SaveChangesAsync();citizen=c.Id;
            }
            var battle=await Battle(v,citizen);string saved;
            await using(var db=await Db())saved=JsonSerializer.Serialize((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/goto",new{spot=new{x=20.5,y=13.5}}),"goto.retreatRequired");
            await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(v.Path+"/me/rest",new{resting=true}),"rest.retreatRequired");
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}),"goto.retreatRequired");
            await using(var db=await Db())Assert.Equal(saved,JsonSerializer.Serialize((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters));
            Assert.Equal(HttpStatusCode.OK,(await v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="hold"})).StatusCode);
            await OkJsonAsync(await v.Tsar.GetAsync(v.Path+"/economy"));
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="enable-work-contracts"}));
            await using(var db=await Db())
            {
                var f=Assert.Single((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);
                Assert.False(f.IsTsar);Assert.Equal(citizen,f.CitizenId);Assert.Equal(7,f.Health);Assert.Equal(15.5,f.X);
            }
            Assert.Equal(HttpStatusCode.OK,(await v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="retreat"})).StatusCode);
            await using(var db=await Db())Assert.Equal("retreating",Assert.Single((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters).Mode);
            await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(v.Path+"/me/rest",new{resting=true}),"rest.retreatRequired");
            await using(var db=await Db())
            {
                var fight=await db.RaidBattles.SingleAsync(b=>b.Id==battle);fight.FinishedAt=factory.Clock.GetUtcNow();await db.SaveChangesAsync();
            }
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}));
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/me/dungeon",new{@class="Brawler"}),"dungeon.busy");
            await using(var db=await Db())Assert.Single((await db.DungeonRuns.SingleAsync(r=>r.VillageId==db.Villages.Single(w=>w.Slug==v.Slug).Id)).Heroes);
        }
        finally{await Cleanup(v);}
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dungeon_reservations_block_new_raid_orders_for_both_roles_and_allow_class_switching(bool marching)
    {
        var v=await players.FoundVillageAsync("Reserved party");try
        {
            var c=await players.JoinAsync(v);var battle=await Battle(v);
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}));
            await OkJsonAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/dungeon",new{@class="Brawler"}));
            await OkJsonAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/dungeon",new{@class="Brawler"}));
            if(marching)await using(var db=await Db())
            {
                var run=await db.DungeonRuns.SingleAsync(r=>r.VillageId==(db.Villages.Single(w=>w.Slug==v.Slug).Id));
                run.Phase=DungeonPhase.Marching;run.MarchEndsAt=factory.Clock.GetUtcNow().AddHours(1);await db.SaveChangesAsync();
            }
            foreach(var client in new[]{v.Tsar,c.Client})
            {
                await AssertRuleBrokenAsync(await client.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="hold"}),"dungeon.busy");
                await AssertRuleBrokenAsync(await client.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="heal",requestId=Guid.NewGuid()}),"dungeon.busy");
            }
            await using var read=await Db();Assert.Empty((await read.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);
            Assert.NotNull((await players.LoadCitizenAsync(c.Id)).DungeonRunId);
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task A_legacy_fighter_present_when_gathering_ends_is_not_removed_or_teleported_by_the_march()
    {
        var v=await players.FoundVillageAsync("Old overlapping fight");try
        {
            using var frozen=factory.Clock.Freeze();
            var c=await players.JoinAsync(v);
            await players.ArrangeVillageAsync(v.Slug,(w,now)=>{w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));w.Store(ResourceKind.Spear,1);});
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}));
            await OkJsonAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/dungeon",new{@class="Spearman"}));
            var battle=await Battle(v,c.Id);var royalBattle=await Battle(v,royal:true);
            string before,royalBefore;
            await using(var db=await Db())
            {
                before=JsonSerializer.Serialize((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);
                royalBefore=JsonSerializer.Serialize((await db.RaidBattles.SingleAsync(b=>b.Id==royalBattle)).Fighters);
            }
            factory.Clock.Advance(TimeSpan.FromSeconds(61));
            Assert.DoesNotContain(await factory.Services.GetRequiredService<GameService>().AdvanceDungeonsAsync(default),start=>start.Slug==v.Slug);
            await using var read=await Db();
            Assert.Equal(before,JsonSerializer.Serialize((await read.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters));
            Assert.Equal(royalBefore,JsonSerializer.Serialize((await read.RaidBattles.SingleAsync(b=>b.Id==royalBattle)).Fighters));
            var w=await read.Villages.SingleAsync(w=>w.Slug==v.Slug);
            var run=await read.DungeonRuns.SingleAsync(r=>r.VillageId==w.Id);
            Assert.Equal(DungeonPhase.Ended,run.Phase);Assert.Equal(DungeonOutcome.Abandoned,run.Outcome);
            Assert.Null(run.CheckpointJson);Assert.Null(factory.Services.GetRequiredService<DungeonRuntime>().Find(run.Id));
            Assert.Equal(1,w.Stock(ResourceKind.Spear));Assert.Null((await players.LoadCitizenAsync(c.Id)).DungeonRunId);
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task Concurrent_orders_to_different_live_raids_cannot_create_two_royal_fighters()
    {
        var v=await players.FoundVillageAsync("Two raid race");try
        {
            var first=await Battle(v);var second=await Battle(v);
            var answers=await Task.WhenAll(new[]{first,second}.Select(id=>v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{id}/order",new{mode="hold"})));
            Assert.Single(answers,r=>r.IsSuccessStatusCode);
            await AssertRuleBrokenAsync(Assert.Single(answers,r=>!r.IsSuccessStatusCode),"raid.queued");
            await using var db=await Db();var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);
            var battles=await db.RaidBattles.Where(b=>b.VillageId==w.Id).ToArrayAsync();
            Assert.Single(battles.SelectMany(b=>b.Fighters),f=>f.Active);
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task Concurrent_royal_raid_order_and_dungeon_open_admit_exactly_one_body()
    {
        var v=await players.FoundVillageAsync("Admission race");try
        {
            var battle=await Battle(v);
            var answers=await Task.WhenAll(v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}),
                v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="hold"}));
            Assert.Single(answers,r=>r.IsSuccessStatusCode);
            var loser=Assert.Single(answers,r=>!r.IsSuccessStatusCode);
            var code=JsonDocument.Parse(await loser.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();
            Assert.Contains(code,new[]{"goto.retreatRequired","dungeon.busy"});
            await using var db=await Db();var b=await db.RaidBattles.SingleAsync(b=>b.Id==battle);
            var run=await db.DungeonRuns.SingleOrDefaultAsync(r=>r.VillageId==b.VillageId);
            Assert.True(run is null?Assert.Single(b.Fighters).Active:b.Fighters.Count==0);
        }
        finally{await Cleanup(v);}
    }
}
