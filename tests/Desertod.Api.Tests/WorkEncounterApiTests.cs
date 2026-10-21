using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Desertod.Api.Auth;
using Desertod.Api.Game;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class WorkEncounterApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private GameService Game=>factory.Services.GetRequiredService<GameService>();
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private sealed class Caller(Guid account):HubCallerContext
    {
        public override string ConnectionId=>"work-encounter-test";
        public override string? UserIdentifier=>account.ToString();
        public override ClaimsPrincipal User { get; }=new(new ClaimsIdentity([new Claim(AccountClaims.AccountId,account.ToString())],"test"));
        public override IDictionary<object,object?> Items { get; }=new Dictionary<object,object?>();
        public override IFeatureCollection Features { get; }=new FeatureCollection();
        public override CancellationToken ConnectionAborted=>default;
        public override void Abort(){}
    }
    private async Task<(FoundedVillage V,Resident A,Guid Order,DateTimeOffset End)> Accepted()
    {
        var v=await players.FoundVillageAsync("Work encounter");var a=await players.JoinAsync(v,"Hunter");
        await OkJsonAsync(await v.Tsar.GetAsync(v.Path+"/economy"));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/economy"));
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);var world=await db.Villages.SingleAsync(w=>w.Id==c.VillageId);
            world.CreatedAt=factory.Clock.GetUtcNow()-CombatRules.PeacefulTime-TimeSpan.FromDays(1);
            world.Storages.Add(StorageBuilding.Create(StorageKind.TownHall,"13,5",factory.Clock.GetUtcNow()));
            world.Storages.Add(StorageBuilding.Create(StorageKind.Granary,"17,5",factory.Clock.GetUtcNow()));
            c.Stats.Set(Stat.Strength,10);c.Stats.Set(Stat.Endurance,10);
            (await db.KingdomEconomies.SingleAsync(e=>e.VillageId==world.Id)).TreasuryCoins=100;await db.SaveChangesAsync();
        }
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="enable-work-contracts"}));
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="publish-work-order",taskId="hunt",cycles=1,pay=10,expiresAt=factory.Clock.GetUtcNow().AddHours(3)}));
        Guid order;
        await using(var db=await Db())order=await db.WorkOrders.Where(o=>o.VillageId==db.Villages.Single(w=>w.Slug==v.Slug).Id).Select(o=>o.Id).SingleAsync();
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="hunt",workOrderId=order,requestId=Guid.NewGuid()}));
        DateTimeOffset end;
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);var world=await db.Villages.SingleAsync(w=>w.Id==c.VillageId);var danger=GameCatalog.FindTask("hunt")!.Danger!;
            c.CycleEncounterSeed=Enumerable.Range(1,10000).First(seed=>CombatRules.SelectAmbush(world,danger,DungeonRandom.FromSeed(seed),c.CycleStartedAt!.Value)?.Kind==EnemyKind.Wolf);
            end=c.CycleEndsAt!.Value;await db.SaveChangesAsync();
        }
        return(v,a,order,end);
    }
    private async Task<Guid> Enter(FoundedVillage v,Resident a,DateTimeOffset end)
    {
        factory.Clock.Advance(end-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
        var me=await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));Assert.Equal("Paused",me.GetProperty("state").GetString());
        return me.GetProperty("encounterRunId").GetGuid();
    }
    private async Task<(DungeonRuntime Runtime,LiveDungeon Live)> Freeze(Guid runId)
    {
        var global=factory.Services.GetRequiredService<DungeonRuntime>();
        if(global.Find(runId) is {} old)
        {
            await old.Operation.WaitAsync();try{global.Remove(runId);}finally{old.Operation.Release();}
        }
        // Claim any not-yet-started march. A background phase pass that already selected it
        // finds Running under the same world lock and cannot register a second live fight.
        await Game.AdvanceDungeonsAsync(default);
        if(global.Find(runId) is {} started)
        {await started.Operation.WaitAsync();try{global.Remove(runId);}finally{started.Operation.Release();}}
        var start=Assert.Single(await Game.RestoreDungeonsAsync(default),s=>s.RunId==runId);
        var runtime=new DungeonRuntime();return(runtime,runtime.Start(start));
    }
    private async Task Tick(DungeonRuntime runtime,LiveDungeon live)
    {
        using var worker=ActivatorUtilities.CreateInstance<DungeonWorker>(factory.Services,runtime,NullLogger<DungeonWorker>.Instance);
        live.LastTickTimestamp=0;
        await (Task)typeof(DungeonWorker).GetMethod("TickDurableAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(worker,[live,.5,CancellationToken.None])!;
    }
    private async Task Cleanup(FoundedVillage v)
    {
        await using var db=await Db();var world=await db.Villages.Where(w=>w.Slug==v.Slug).Select(w=>w.Id).SingleAsync();
        var global=factory.Services.GetRequiredService<DungeonRuntime>();
        foreach(var id in await db.DungeonRuns.Where(r=>r.VillageId==world).Select(r=>r.Id).ToArrayAsync())
            if(global.Find(id) is {} live){await live.Operation.WaitAsync();try{global.Remove(id);}finally{live.Operation.Release();}}
        await db.Villages.Where(w=>w.Id==world).ExecuteDeleteAsync();
    }
    [Fact]
    public async Task Accepted_fund_waits_for_manual_victory_then_pays_once_and_enters_requested_rest()
    {
        var s=await Accepted();try
        {
            await OkJsonAsync(await s.A.Client.PutAsJsonAsync(s.V.Path+"/me/rest",new{resting=true}));
            var runId=await Enter(s.V,s.A,s.End);var frozen=await Freeze(runId);
            await using(var db=await Db())
            {
                var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);Assert.False(c.CycleSettled);
                Assert.Equal(WorkClaimStatus.Working,(await db.WorkOrderClaims.SingleAsync(c=>c.WorkOrderId==s.Order)).Status);
                Assert.Equal(10,await db.WorkOrders.Where(o=>o.Id==s.Order).Select(o=>o.ReservedCoins).SingleAsync());
                Assert.Equal(0,(await db.GameWallets.SingleAsync(w=>w.VillageId==c.VillageId&&w.AccountId==c.AccountId)).Coins);
                Assert.Equal(1,await db.DungeonRuns.CountAsync(r=>r.WorkCitizenId==c.Id));
            }
            Assert.DoesNotContain(await Game.FindDueCitizensAsync(1000,default),x=>x.CitizenId==s.A.Id);
            var hero=frozen.Live.Simulation.Heroes.Single();var hub=new DungeonHub(frozen.Runtime,Game){Context=new Caller(hero.Fighter.AccountId)};
            Assert.True(await hub.Attack(runId,hero.Id,0));
            for(var i=0;i<30&&!frozen.Live.Simulation.IsOver;i++)await Tick(frozen.Runtime,frozen.Live);
            Assert.Equal(DungeonOutcome.Victory,frozen.Live.Simulation.Outcome);
            for(var i=0;i<2;i++)await OkJsonAsync(await s.A.Client.GetAsync(s.V.Path+"/me"));
            await using(var db=await Db())
            {
                var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);Assert.True(c.CycleSettled);Assert.Equal(RestState.Resting,c.RestState);
                Assert.Equal(10,(await db.GameWallets.SingleAsync(w=>w.VillageId==c.VillageId&&w.AccountId==c.AccountId)).Coins);
                Assert.Equal(WorkClaimStatus.Paid,(await db.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==s.Order)).Status);
                Assert.Equal(0,(await db.WorkOrders.SingleAsync(o=>o.Id==s.Order)).ReservedCoins);
                Assert.True(WorkEncounterRules.Report(c)!.Won);
            }
        }finally{await Cleanup(s.V);}
    }
    [Fact]
    public async Task Escape_returns_no_wage_or_food_and_actor_cannot_use_rest_cancel_or_goto_as_an_escape()
    {
        var s=await Accepted();try
        {
            var run=await Enter(s.V,s.A,s.End);var f=await Freeze(run);
            await AssertRuleBrokenAsync(await s.A.Client.PutAsJsonAsync(s.V.Path+"/me/rest",new{resting=true}),"rest.dungeonRequired");
            await AssertRuleBrokenAsync(await s.A.Client.DeleteAsync(s.V.Path+"/me/task"),"ambush.retreatRequired");
            await AssertRuleBrokenAsync(await s.A.Client.PostAsJsonAsync(s.V.Path+"/me/goto",new{spot=new{x=15.5,y=13.5}}),"dungeon.busy");
            var hero=f.Live.Simulation.Heroes.Single();var hub=new DungeonHub(f.Runtime,Game){Context=new Caller(hero.Fighter.AccountId)};
            Assert.True(await hub.Retreat(run,hero.Id));for(var i=0;i<20&&!f.Live.Simulation.IsOver;i++)await Tick(f.Runtime,f.Live);
            await using var db=await Db();var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);
            Assert.True(c.CycleSettled);Assert.False(WorkEncounterRules.Report(c)!.Won);Assert.Null(c.InjuredUntil);
            Assert.Equal(0,(await db.GameWallets.SingleAsync(w=>w.VillageId==c.VillageId&&w.AccountId==c.AccountId)).Coins);
            Assert.Equal(WorkClaimStatus.Unproductive,(await db.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==s.Order)).Status);
        }finally{await Cleanup(s.V);}
    }
    [Fact]
    public async Task Private_encounter_does_not_replace_the_public_gathering_or_allow_another_account_to_watch()
    {
        var s=await Accepted();try
        {
            var run=await Enter(s.V,s.A,s.End);await Freeze(run);
            var other=await players.JoinAsync(s.V,"Other");Guid otherAccount;
            await using(var db=await Db())otherAccount=await db.Citizens.Where(c=>c.Id==other.Id).Select(c=>c.AccountId).SingleAsync();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Game.GetDungeonRunAsync(run,otherAccount,default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new DungeonHub(new DungeonRuntime(),Game){Context=new Caller(otherAccount)}.JoinRun(run));
            var opened=await OkJsonAsync(await s.V.Tsar.PostAsJsonAsync(s.V.Path+"/dungeon",new{dungeonId="dead-lands"}));
            Assert.NotEqual(run,opened.GetProperty("dungeon").GetProperty("id").GetGuid());
            await OkJsonAsync(await other.Client.PostAsJsonAsync(s.V.Path+"/me/dungeon",new{@class="Brawler"}));
            var full=await OkJsonAsync(await s.A.Client.GetAsync(s.V.Path));
            Assert.NotEqual(run,full.GetProperty("state").GetProperty("dungeon").GetProperty("id").GetGuid());
        }finally{await Cleanup(s.V);}
    }
    [Fact]
    public async Task Migration_refuses_to_drop_an_accepted_encounter_and_keeps_its_fund_and_checkpoint()
    {
        // Exercise this guard independently: a newer financial-history guard legitimately
        // refuses the full chain first. Execute EF's exact old Down script in a rollback-only
        // transaction; neither newer receipts nor the shared fixture's schema are removed.
        var isolated=new DesertodApiFactory();await isolated.InitializeAsync();
        try
        {
            var probe=new WorkEncounterApiTests(isolated);var s=await probe.Accepted();
            var run=await probe.Enter(s.V,s.A,s.End);await probe.Freeze(run);
            await using var db=await probe.Db();var before=(await db.DungeonRuns.AsNoTracking().SingleAsync(r=>r.Id==run)).CheckpointJson;
            var applied=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var script=db.GetService<IMigrator>().GenerateScript("20261021011454_WorkEncounters",
                "20261021000703_InterruptedTravel",MigrationsSqlGenerationOptions.NoTransactions);
            await using(var transaction=await db.Database.BeginTransactionAsync())
            {
                try
                {
                    var error=await Assert.ThrowsAnyAsync<Exception>(()=>db.Database.ExecuteSqlRawAsync(script));
                    Assert.Contains("Work encounters or accepted encounter cycles exist",error.ToString());
                }
                finally{await transaction.RollbackAsync();}
            }
            Assert.Equal(before,await db.DungeonRuns.AsNoTracking().Where(r=>r.Id==run).Select(r=>r.CheckpointJson).SingleAsync());
            Assert.Equal(10,(await db.WorkOrders.SingleAsync(o=>o.Id==s.Order)).ReservedCoins);
            Assert.Equal(applied,(await db.Database.GetAppliedMigrationsAsync()).ToArray());
            await OkJsonAsync(await s.V.Tsar.GetAsync(s.V.Path));
            await using var shared=await Db();
            Assert.Equal(applied,(await shared.Database.GetAppliedMigrationsAsync()).ToArray());
            _=await shared.Villages.AsNoTracking().FirstAsync();
        }
        finally{await ((IAsyncLifetime)isolated).DisposeAsync();}
    }

    [Fact]
    public async Task Pre_version_accepted_work_keeps_its_original_instant_settlement_contract()
    {
        var s=await Accepted();try
        {
            await using(var db=await Db()){var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);c.CycleEncounterVersion=0;await db.SaveChangesAsync();}
            factory.Clock.Advance(s.End-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
            var me=await OkJsonAsync(await s.A.Client.GetAsync(s.V.Path+"/me"));Assert.Equal(System.Text.Json.JsonValueKind.Null,me.GetProperty("encounterRunId").ValueKind);
            await using var check=await Db();Assert.True((await check.Citizens.SingleAsync(c=>c.Id==s.A.Id)).CycleSettled);
            Assert.Empty(await check.DungeonRuns.Where(r=>r.WorkCitizenId==s.A.Id).ToArrayAsync());
        }finally{await Cleanup(s.V);}
    }

    [Fact]
    public async Task Final_outcome_commit_failure_rolls_back_rewards_and_exact_retry_settles_one_claim()
    {
        var s=await Accepted();try
        {
            var runId=await Enter(s.V,s.A,s.End);var f=await Freeze(runId);
            var simulation=DungeonSimulation.Restore(f.Live.Save!.Fight);var random=new DungeonRandom(f.Live.Save.RandomState);var hero=simulation.Heroes.Single();
            simulation.Attack(hero.Id,0);for(var i=0;i<100&&!simulation.IsOver;i++)simulation.Tick(.1,random);
            Assert.Equal(DungeonOutcome.Victory,simulation.Outcome);
            var next=f.Live.Save with{Revision=f.Live.Save.Revision+1,Fight=simulation.Capture(),RandomState=random.State,Settled=[hero.Id]};
            var exits=new[]{new DungeonExit(hero.Id,simulation.OutcomeOf(hero))};
            var name="reject_work_result_"+Guid.NewGuid().ToString("N");await using var triggerDb=await Db();
            // Generated identifiers/UUID only; fail the actual commit after every accounting write.
#pragma warning disable EF1002
            await triggerDb.Database.ExecuteSqlRawAsync($"CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Id\"='{runId}'::uuid THEN RAISE EXCEPTION 'Synthetic work encounter commit failure'; END IF; RETURN NEW; END; $$; CREATE CONSTRAINT TRIGGER {name} AFTER UPDATE ON \"DungeonRuns\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION {name}();");
            try{await Assert.ThrowsAnyAsync<Exception>(()=>Game.SettleDungeonAsync(runId,f.Live.VillageId,exits,simulation.Outcome,simulation.Slain,default,next));}
            finally{await triggerDb.Database.ExecuteSqlRawAsync($"DROP TRIGGER {name} ON \"DungeonRuns\"; DROP FUNCTION {name}();");}
#pragma warning restore EF1002
            await using(var db=await Db())
            {
                var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);Assert.False(c.CycleSettled);Assert.Null(c.CycleEncounterReportJson);Assert.Equal(runId,c.DungeonRunId);
                Assert.Equal(0,(await db.GameWallets.SingleAsync(w=>w.VillageId==c.VillageId&&w.AccountId==c.AccountId)).Coins);
                Assert.Equal(WorkClaimStatus.Working,(await db.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==s.Order)).Status);
            }
            await Game.SettleDungeonAsync(runId,f.Live.VillageId,exits,simulation.Outcome,simulation.Slain,default,next);
            await Game.SettleDungeonAsync(runId,f.Live.VillageId,exits,simulation.Outcome,simulation.Slain,default,next);
            await using var final=await Db();var person=await final.Citizens.SingleAsync(c=>c.Id==s.A.Id);
            Assert.True(person.CycleSettled);Assert.Equal(10,(await final.GameWallets.SingleAsync(w=>w.VillageId==person.VillageId&&w.AccountId==person.AccountId)).Coins);
            Assert.Equal(WorkClaimStatus.Paid,(await final.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==s.Order)).Status);
            Assert.Equal(0,(await final.WorkOrders.SingleAsync(o=>o.Id==s.Order)).ReservedCoins);
        }finally{await Cleanup(s.V);}
    }

    [Fact]
    public async Task A_late_native_wound_defers_the_selected_work_encounter_without_hot_loop_or_payment()
    {
        var s=await Accepted();try
        {
            await using(var db=await Db()){var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);c.InjuredUntil=s.End.AddMinutes(5);await db.SaveChangesAsync();}
            factory.Clock.Advance(s.End-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
            var me=await OkJsonAsync(await s.A.Client.GetAsync(s.V.Path+"/me"));Assert.Equal(System.Text.Json.JsonValueKind.Null,me.GetProperty("encounterRunId").ValueKind);
            Assert.DoesNotContain(await Game.FindDueCitizensAsync(1000,default),x=>x.CitizenId==s.A.Id);
            await using(var db=await Db()){var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);Assert.False(c.CycleSettled);Assert.False(c.CycleAmbushChecked);}
            factory.Clock.Advance(TimeSpan.FromMinutes(6));
            var healed=await OkJsonAsync(await s.A.Client.GetAsync(s.V.Path+"/me"));var run=healed.GetProperty("encounterRunId").GetGuid();await Freeze(run);
        }finally{await Cleanup(s.V);}
    }
}
