using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class GroundRouteApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task Cleanup(FoundedVillage v)
    {
        await using var db=await Db();var id=await db.Villages.Where(w=>w.Slug==v.Slug).Select(w=>w.Id).SingleAsync();
        var runtime=factory.Services.GetRequiredService<DungeonRuntime>();
        foreach(var run in await db.DungeonRuns.Where(r=>r.VillageId==id).Select(r=>r.Id).ToArrayAsync())
            if(runtime.Find(run) is {} live){await live.Operation.WaitAsync();try{runtime.Remove(run);}finally{live.Operation.Release();}}
        await db.Villages.Where(w=>w.Id==id).ExecuteDeleteAsync();
    }
    private async Task<(FoundedVillage V,Resident A,Guid Order)> Funded()
    {
        var v=await players.FoundVillageAsync("Funded core path");var a=await players.JoinAsync(v,"Worker");
        await OkJsonAsync(await v.Tsar.GetAsync(v.Path+"/economy"));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/economy"));
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);
            (await db.KingdomEconomies.SingleAsync(e=>e.VillageId==c.VillageId)).TreasuryCoins=100;await db.SaveChangesAsync();
        }
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="enable-work-contracts"}));
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="publish-work-order",taskId="chop-wood",cycles=1,pay=10,expiresAt=factory.Clock.GetUtcNow().AddHours(1)}));
        await using var read=await Db();var world=await read.Citizens.Where(c=>c.Id==a.Id).Select(c=>c.VillageId).SingleAsync();
        return(v,a,await read.WorkOrders.Where(o=>o.VillageId==world).Select(o=>o.Id).SingleAsync());
    }

    [Fact]
    public async Task Resident_and_royal_core_orders_save_real_detours_and_rejected_targets_keep_the_saved_route()
    {
        var v=await players.FoundVillageAsync("Core detour");try
        {
            var a=await players.JoinAsync(v,"Walker");
            await players.ArrangeVillageAsync(v.Slug,(w,_)=>{for(var y=9;y<=17;y++)w.Fortifications.Add(new(){Id=$"core-{y}",X=11,Y=y});});
            var target=new TilePoint(8.5,13.5);
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/goto",new{spot=target}));
            await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/goto",new{spot=target}));
            string beforeRoyal,beforeResident;byte[] beforeFog;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);
                foreach(var trip in new[]{w.TsarTrip!,c.Trip!})
                {
                    Assert.NotEmpty(trip.Waypoints);Assert.True(trip.Duration>trip.From.DistanceTo(target)*Exploration.SecondsPerTile);
                    Assert.Equal(target,trip.PositionAt(trip.ArrivesAt));
                }
                beforeRoyal=JsonSerializer.Serialize(w.TsarTrip);beforeResident=JsonSerializer.Serialize(c.Trip);beforeFog=(byte[])w.Explored.Clone();
            }
            var blocked=new TilePoint(11.5,13.5);
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/goto",new{spot=blocked}),"spot.noPath");
            await AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/goto",new{spot=blocked}),"spot.noPath");
            await using var check=await Db();var world=await check.Villages.SingleAsync(w=>w.Slug==v.Slug);
            Assert.Equal(beforeRoyal,JsonSerializer.Serialize(world.TsarTrip));Assert.Equal(beforeResident,JsonSerializer.Serialize((await check.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip));
            Assert.Equal(beforeFog,world.Explored);
            var me=await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));Assert.True(me.GetProperty("journey").GetProperty("waypoints").GetArrayLength()>1);
        }finally{await Cleanup(v);}
    }

    [Fact]
    public async Task An_unreachable_accepted_order_debits_nothing_and_the_same_request_can_succeed_after_the_path_opens()
    {
        var s=await Funded();try
        {
            await players.ArrangeVillageAsync(s.V.Slug,(w,_)=>w.Fortifications.Add(new(){Id="blocked-work",X=5,Y=8}));
            double energy;long treasury;
            await using(var db=await Db())
            {
                energy=(await db.Citizens.SingleAsync(c=>c.Id==s.A.Id)).StoredEnergy;
                treasury=(await db.KingdomEconomies.SingleAsync(e=>e.VillageId==db.Citizens.Single(c=>c.Id==s.A.Id).VillageId)).TreasuryCoins;
            }
            var request=new{taskId="chop-wood",workOrderId=s.Order,requestId=Guid.NewGuid()};
            await AssertRuleBrokenAsync(await s.A.Client.PostAsJsonAsync(s.V.Path+"/me/task",request),"spot.noPath");
            await using(var db=await Db())
            {
                var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);Assert.Null(c.CurrentTaskId);Assert.Null(c.Trip);Assert.Equal(energy,c.StoredEnergy);
                Assert.Empty(await db.WorkOrderClaims.Where(x=>x.WorkOrderId==s.Order).ToArrayAsync());
                Assert.False(await db.EconomyReceipts.AnyAsync(r=>r.RequestId==request.requestId));
                var order=await db.WorkOrders.SingleAsync(o=>o.Id==s.Order);Assert.Equal(1,order.AvailableCycles);Assert.Equal(10,order.ReservedCoins);
                Assert.Equal(treasury,(await db.KingdomEconomies.SingleAsync(e=>e.VillageId==c.VillageId)).TreasuryCoins);
                var w=await db.Villages.SingleAsync(w=>w.Id==c.VillageId);w.Fortifications.RemoveAll(f=>f.Id=="blocked-work");await db.SaveChangesAsync();
            }
            await OkJsonAsync(await s.A.Client.PostAsJsonAsync(s.V.Path+"/me/task",request));
            Trip first;double acceptedEnergy;
            await using(var db=await Db()){var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);first=c.Trip!;acceptedEnergy=c.StoredEnergy;Assert.NotEmpty(first.Waypoints);}
            await OkJsonAsync(await s.A.Client.PostAsJsonAsync(s.V.Path+"/me/task",request));
            await using var final=await Db();var accepted=await final.Citizens.SingleAsync(c=>c.Id==s.A.Id);
            Assert.Equal(first.Id,accepted.Trip!.Id);Assert.Equal(acceptedEnergy,accepted.StoredEnergy);
            Assert.Equal(1,await final.WorkOrderClaims.CountAsync(x=>x.WorkOrderId==s.Order));
        }finally{await Cleanup(s.V);}
    }

    [Fact]
    public async Task A_paid_core_job_waits_at_the_gate_and_preserves_its_fund_until_explicit_resume_and_rest()
    {
        var s=await Funded();try
        {
            await OkJsonAsync(await s.A.Client.PostAsJsonAsync(s.V.Path+"/me/task",new{taskId="chop-wood",workOrderId=s.Order,requestId=Guid.NewGuid()}));
            Trip original;DateTimeOffset started,end;
            await using(var db=await Db())
            {
                var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);original=c.Trip!;started=c.CycleStartedAt!.Value;end=c.CycleEndsAt!.Value;
                Assert.NotEmpty(original.Waypoints);Assert.True(WorldMap.LegacyLand.Contains(original.From.ToTile()));Assert.True(WorldMap.LegacyLand.Contains(original.To.ToTile()));
            }
            var point=original.PositionAt(original.SetOutAt+(original.ArrivesAt-original.SetOutAt)/2);
            await players.ArrangeVillageAsync(s.V.Slug,(w,_)=>w.Fortifications.Add(new(){Id="core-gate",Gate=true,Open=true,X=(int)Math.Floor(point.X),Y=(int)Math.Floor(point.Y)}));
            Assert.Equal(HttpStatusCode.OK,(await s.V.Tsar.PutAsJsonAsync(s.V.Path+"/gate",new{open=false})).StatusCode);
            Trip stopped;
            await using(var db=await Db()){stopped=(await db.Citizens.SingleAsync(c=>c.Id==s.A.Id)).Trip!;Assert.NotNull(stopped.BlockedAt);}
            factory.Clock.Advance(end-factory.Clock.GetUtcNow()+TimeSpan.FromMinutes(1));
            var me=await OkJsonAsync(await s.A.Client.GetAsync(s.V.Path+"/me"));Assert.Equal("Paused",me.GetProperty("state").GetString());
            await using(var db=await Db())
            {
                Assert.Equal(10,(await db.WorkOrders.SingleAsync(o=>o.Id==s.Order)).ReservedCoins);
                Assert.Equal(WorkClaimStatus.Working,(await db.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==s.Order)).Status);
                Assert.Equal(0,(await db.GameWallets.SingleAsync(w=>w.AccountId==db.Citizens.Single(c=>c.Id==s.A.Id).AccountId&&w.VillageId==db.Citizens.Single(c=>c.Id==s.A.Id).VillageId)).Coins);
            }
            await OkJsonAsync(await s.A.Client.PutAsJsonAsync(s.V.Path+"/me/rest",new{resting=true}));
            var request=new ResumeJourneyRequest(Guid.NewGuid(),stopped.Id,stopped.BlockedAt!.Value);
            for(var i=0;i<2;i++)await OkJsonAsync(await s.A.Client.PostAsJsonAsync(s.V.Path+"/me/journey/resume",request));
            await using(var db=await Db())
            {
                var c=await db.Citizens.SingleAsync(c=>c.Id==s.A.Id);Assert.Equal(started,c.CycleStartedAt);Assert.Null(c.Trip!.BlockedAt);
                Assert.True(c.Trip.From.DistanceTo(stopped.PositionAt(factory.Clock.GetUtcNow()))<.0001);end=c.CycleEndsAt!.Value;
            }
            factory.Clock.Advance(end-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
            for(var i=0;i<2;i++)await OkJsonAsync(await s.A.Client.GetAsync(s.V.Path+"/me"));
            await using var final=await Db();var worker=await final.Citizens.SingleAsync(c=>c.Id==s.A.Id);
            Assert.True(worker.CycleSettled);Assert.Equal(RestState.Resting,worker.RestState);
            Assert.Equal(WorkClaimStatus.Paid,(await final.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==s.Order)).Status);
            Assert.Equal(0,(await final.WorkOrders.SingleAsync(o=>o.Id==s.Order)).ReservedCoins);
            Assert.Equal(10,(await final.GameWallets.SingleAsync(w=>w.AccountId==worker.AccountId&&w.VillageId==worker.VillageId)).Coins);
        }finally{await Cleanup(s.V);}
    }
    [Fact]
    public async Task A_changed_departure_path_invalidates_the_directors_proposal_and_removes_the_fresh_option()
    {
        var v=await players.FoundVillageAsync("Changed dungeon approach");try
        {
            for(var i=0;i<3;i++)
            {
                var resident=await players.JoinAsync(v,"Ready"+i);
                factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id,v.Slug);
            }
            var game=factory.Services.GetRequiredService<GameService>();
            Guid world;
            await using(var db=await Db())world=await db.Villages.Where(w=>w.Slug==v.Slug).Select(w=>w.Id).SingleAsync();
            var review=(await game.BeginAlmightyReviewAsync(world,default))!;Assert.Contains(review.Options,o=>o.Id=="dungeon");
            Assert.True(await game.CompleteAlmightyReviewAsync(review,new("dungeon","Общий поход.","Подготовить сбор."),default));
            Guid proposal,owner;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Id==world);owner=w.OwnerAccountId;
                proposal=await db.AlmightyProposals.Where(p=>p.VillageId==world).Select(p=>p.Id).SingleAsync();
                for(var y=WorldMap.Land.Y;y<WorldMap.Land.Y+WorldMap.Land.Height;y++)w.Fortifications.Add(new(){Id=$"closed-{y}",X=11,Y=y});
                await db.SaveChangesAsync();
            }
            await game.DecideAlmightyAsync(proposal,owner,null,true,default);
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}),"spot.noPath");
            await using(var db=await Db())
            {
                Assert.Equal("Superseded",(await db.AlmightyProposals.SingleAsync(p=>p.Id==proposal)).Status);
                Assert.False(await db.DungeonRuns.AnyAsync(r=>r.VillageId==world));
            }
            await game.SetAlmightyModeAsync(v.Slug,owner,"Propose",default);
            Assert.DoesNotContain((await game.BeginAlmightyReviewAsync(world,default))!.Options,o=>o.Id=="dungeon");
        }finally{await Cleanup(v);}
    }

    [Fact]
    public async Task A_gate_closed_during_the_group_march_never_creates_a_combat_checkpoint_for_blocked_heroes()
    {
        using var frozenTime=factory.Clock.Freeze();
        var v=await players.FoundVillageAsync("Blocked group march");try
        {
            var a=await players.JoinAsync(v,"Walker");
            await players.ArrangeVillageAsync(v.Slug,(w,_)=>{
                for(var y=WorldMap.Land.Y;y<WorldMap.Land.Y+WorldMap.Land.Height;y++)w.Fortifications.Add(new(){Id=$"march-{y}",X=11,Y=y,Gate=y==13,Open=y==13});
            });
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}));
            await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/dungeon",new{@class="Brawler"}));
            var game=factory.Services.GetRequiredService<GameService>();
            factory.Clock.Advance(DungeonRules.Gathering+TimeSpan.FromSeconds(1));
            Assert.Empty(await game.AdvanceDungeonsAsync(default));
            Guid run;DateTimeOffset due;
            await using(var db=await Db())
            {
                var r=await db.DungeonRuns.SingleAsync(r=>r.VillageId==db.Villages.Single(w=>w.Slug==v.Slug).Id);
                run=r.Id;due=r.MarchEndsAt!.Value;Assert.Equal(DungeonPhase.Marching,r.Phase);
            }
            Assert.Equal(HttpStatusCode.OK,(await v.Tsar.PutAsJsonAsync(v.Path+"/gate",new{open=false})).StatusCode);
            Trip frozen;
            await using(var db=await Db()){frozen=(await db.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip!;Assert.NotNull(frozen.BlockedAt);}
            factory.Clock.Advance(due-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
            Assert.Empty(await game.AdvanceDungeonsAsync(default));
            await using var check=await Db();var ended=await check.DungeonRuns.SingleAsync(r=>r.Id==run);var c=await check.Citizens.SingleAsync(c=>c.Id==a.Id);
            Assert.Equal(DungeonPhase.Ended,ended.Phase);Assert.Equal(DungeonOutcome.Abandoned,ended.Outcome);Assert.Null(ended.CheckpointJson);
            Assert.Null(c.DungeonRunId);Assert.True(c.Trip!.From.DistanceTo(frozen.PositionAt(factory.Clock.GetUtcNow()))<.0001);
            Assert.Equal(WorldMap.Places[WorldMap.Camp],c.Trip.To);Assert.Null(factory.Services.GetRequiredService<DungeonRuntime>().Find(run));
        }finally{await Cleanup(v);}
    }

}
