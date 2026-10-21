using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DirectorResilienceApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private GameService Game=>factory.Services.GetRequiredService<GameService>();
    private IDbContextFactory<DesertodDbContext> Contexts=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>();

    [Fact]
    public async Task A_stalled_model_does_not_lock_the_world_or_delay_accepted_paid_work_and_a_late_answer_cannot_apply()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Director outage work");var worker=await players.JoinAsync(v,"Worker");
        (await v.Tsar.GetAsync(v.Path+"/economy")).EnsureSuccessStatusCode();
        (await worker.Client.GetAsync(v.Path+"/economy")).EnsureSuccessStatusCode();
        Guid worldId;
        await using(var db=await Contexts.CreateDbContextAsync())
        {
            worldId=await db.Villages.Where(w=>w.Slug==v.Slug).Select(w=>w.Id).SingleAsync();
            (await db.KingdomEconomies.SingleAsync(e=>e.VillageId==worldId)).TreasuryCoins=100;await db.SaveChangesAsync();
        }
        (await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="enable-work-contracts"})).EnsureSuccessStatusCode();
        (await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="publish-work-order",taskId="chop-wood",cycles=1,pay=10,expiresAt=factory.Clock.GetUtcNow().AddHours(1)})).EnsureSuccessStatusCode();
        Guid order;
        await using(var db=await Contexts.CreateDbContextAsync())order=await db.WorkOrders.Where(o=>o.VillageId==worldId).Select(o=>o.Id).SingleAsync();
        (await worker.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood",workOrderId=order,requestId=Guid.NewGuid()})).EnsureSuccessStatusCode();
        await using(var db=await Contexts.CreateDbContextAsync())
        {
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==worker.Id);
            citizen.CycleEndsAt=factory.Clock.GetUtcNow().AddSeconds(10);citizen.CycleAmbushChecked=true;await db.SaveChangesAsync();
        }
        var bridge=new DirectorPendingBridge();var deadline=new DirectorDeadlineClock(factory.Clock);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Game:AlmightyEnabled","true"},{"Hosting:PrimaryVillage",v.Slug}}).Build();
        var director=new AlmightyService(Game,Contexts,bridge,config,deadline,NullLogger<AlmightyService>.Instance);
        var tick=director.TickAsync(default);await deadline.Armed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await bridge.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // These real world operations must finish while the adapter is still unresolved.
        (await v.Tsar.GetAsync(v.Path+"/director").WaitAsync(TimeSpan.FromSeconds(30))).EnsureSuccessStatusCode();
        (await worker.Client.PutAsJsonAsync(v.Path+"/me/rest",new{resting=true}).WaitAsync(TimeSpan.FromSeconds(30))).EnsureSuccessStatusCode();
        factory.Clock.Advance(TimeSpan.FromSeconds(11));
        var me=await OkJsonAsync(await worker.Client.GetAsync(v.Path+"/me").WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal("Resting",me.GetProperty("restState").GetString());
        await using(var db=await Contexts.CreateDbContextAsync())
        {
            var accountId=await db.Citizens.Where(c=>c.Id==worker.Id).Select(c=>c.AccountId).SingleAsync();
            var wallet=await db.GameWallets.SingleAsync(w=>w.VillageId==worldId&&w.AccountId==accountId);
            Assert.Equal(10,wallet.Coins);Assert.Equal(10,wallet.EarnedToday);
            Assert.Equal(WorkClaimStatus.Paid,(await db.WorkOrderClaims.SingleAsync(c=>c.WorkOrderId==order)).Status);
            Assert.Equal(0,(await db.WorkOrders.SingleAsync(o=>o.Id==order)).ReservedCoins);
        }
        Assert.False(tick.IsCompleted);deadline.Fire();await tick.WaitAsync(TimeSpan.FromSeconds(30));
        bridge.Pending.SetResult(new("economy_report","Late answer","Observe"));
        await director.TickAsync(default); // Normal next-review gate blocks an immediate retry.
        await using(var db=await Contexts.CreateDbContextAsync())
        {
            var state=await db.AlmightyStates.SingleAsync(s=>s.VillageId==worldId);
            Assert.Null(state.Lease);Assert.Contains("не успел",state.LastError);Assert.Equal(1,state.CallsToday);
            Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==worldId).ToArrayAsync());
            Assert.Equal(10,(await db.GameWallets.SingleAsync(w=>w.VillageId==worldId&&w.AccountId==db.Citizens.Single(c=>c.Id==worker.Id).AccountId)).Coins);
        }
        Assert.Equal(1,bridge.Calls);
    }

    [Fact]
    public async Task Bridge_failure_codes_are_durable_and_never_turn_into_an_overdue_raid_fallback()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Director outage fallback");var residents=new List<Guid>();
        for(var i=0;i<4;i++)residents.Add((await players.JoinAsync(v,"Guard"+i)).Id);
        Guid worldId;
        await using(var db=await Contexts.CreateDbContextAsync())
        {
            var world=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);worldId=world.Id;
            world.CreatedAt=factory.Clock.GetUtcNow().AddDays(-10);world.RaidsPlannedPeriod=long.MaxValue;await db.SaveChangesAsync();
        }
        foreach(var code in new[]{"bridge.rate_limit","bridge.json","bridge.fields","bridge.payload"})
        {
            foreach(var id in residents)factory.Services.GetRequiredService<CitizenPresence>().Touch(id,v.Slug);
            var review=(await Game.BeginAlmightyReviewAsync(worldId,default))!;
            using var snapshot=JsonDocument.Parse(review.Snapshot);
            Assert.True(snapshot.RootElement.GetProperty("raidPressure").GetProperty("proposalDue").GetBoolean());
            Assert.True(await Game.CompleteAlmightyReviewAsync(review,AlmightyAnalysis.Failed(code),default));
            await using(var db=await Contexts.CreateDbContextAsync())
            {
                var state=await db.AlmightyStates.SingleAsync(s=>s.VillageId==worldId);Assert.Null(state.Lease);Assert.NotEmpty(state.LastError);
                Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==worldId).ToArrayAsync());
                Assert.Empty((await db.Villages.SingleAsync(w=>w.Id==worldId)).Raids);
                Assert.Equal(state.LastError,(await db.AlmightyObservations.SingleAsync(o=>o.Id==review.Lease)).Reason);
            }
            Assert.Null(await Game.BeginAlmightyReviewAsync(worldId,default));
            factory.Clock.Advance(TimeSpan.FromMinutes(16));
        }
    }
}
