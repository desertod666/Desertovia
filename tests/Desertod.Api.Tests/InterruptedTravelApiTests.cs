using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class InterruptedTravelApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static Task<HttpResponseMessage> Gate(FoundedVillage v,bool open)=>v.Tsar.PutAsJsonAsync(v.Path+"/gate",new{open});
    private async Task<Trip> Current(Resident a)
    {await using var db=await Db();return (await db.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip!;}
    private static Task<HttpResponseMessage> Resume(Resident a,ResumeJourneyRequest body)=>a.Client.PostAsJsonAsync(a.Path+"/me/journey/resume",body);
    private async Task ArrangeGate(FoundedVillage v,Trip trip)
    {
        var spot=trip.PositionAt(trip.SetOutAt+(trip.ArrivesAt-trip.SetOutAt)/2);
        await players.ArrangeVillageAsync(v.Slug,(world,_)=>world.Fortifications.Add(new Fortification{Id="road-fixture",Gate=true,Open=true,X=(int)Math.Floor(spot.X),Y=(int)Math.Floor(spot.Y)}));
    }
    private async Task<(FoundedVillage V,Resident A)> Walking()
    {
        var v=await players.FoundVillageAsync("Blocked road");var a=await players.JoinAsync(v,"Walker");
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/goto",new{spot=RegionalTerrain.MarshTrail}));
        await ArrangeGate(v,await Current(a));return(v,a);
    }

    [Fact]
    public async Task Funded_work_waits_without_pay_then_resumes_once_and_finishes_the_requested_safe_rest()
    {
        var v=await players.FoundVillageAsync("Funded blocked road");var a=await players.JoinAsync(v,"Worker");
        await OkJsonAsync(await v.Tsar.GetAsync(v.Path+"/economy"));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/economy"));
        await using(var db=await Db())
        {
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==a.Id);citizen.IdleSpot=RegionalTerrain.MarshTrail;
            (await db.KingdomEconomies.SingleAsync(e=>e.VillageId==citizen.VillageId)).TreasuryCoins=100;
            await db.SaveChangesAsync();
        }
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="enable-work-contracts"}));
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="publish-work-order",taskId="chop-wood",cycles=1,pay=10,expiresAt=factory.Clock.GetUtcNow().AddHours(1)}));
        Guid order;
        await using(var db=await Db())order=await db.WorkOrders.Where(o=>o.VillageId==db.Villages.Single(w=>w.Slug==v.Slug).Id).Select(o=>o.Id).SingleAsync();
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood",workOrderId=order,requestId=Guid.NewGuid()}));
        var original=await Current(a);DateTimeOffset started,oldEnd;
        await using(var db=await Db()){var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);started=c.CycleStartedAt!.Value;oldEnd=c.CycleEndsAt!.Value;}
        await ArrangeGate(v,original);Assert.Equal(HttpStatusCode.OK,(await Gate(v,false)).StatusCode);
        var stopped=await Current(a);Assert.NotNull(stopped.BlockedAt);Assert.Equal("gate",stopped.BlockReason);
        factory.Clock.Advance(oldEnd-factory.Clock.GetUtcNow()+TimeSpan.FromMinutes(10));
        var me=await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));Assert.Equal("Paused",me.GetProperty("state").GetString());
        Assert.Equal(stopped.BlockedAt,me.GetProperty("journey").GetProperty("blockedAt").GetDateTimeOffset());
        var due=await factory.Services.GetRequiredService<GameService>().FindDueCitizensAsync(1000,CancellationToken.None);Assert.DoesNotContain(due,c=>c.CitizenId==a.Id);
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);Assert.Equal(0,(await db.GameWallets.SingleAsync(w=>w.AccountId==c.AccountId&&w.VillageId==c.VillageId)).Coins);
            Assert.False(c.CycleSettled);Assert.Equal(WorkClaimStatus.Working,(await db.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==order)).Status);
            Assert.Equal(10,(await db.WorkOrders.SingleAsync(o=>o.Id==order)).ReservedCoins);
        }
        await OkJsonAsync(await a.Client.PutAsJsonAsync(v.Path+"/me/rest",new{resting=true}));
        var request=new ResumeJourneyRequest(Guid.NewGuid(),stopped.Id,stopped.BlockedAt!.Value);
        // A single closed gate can be walked around. Explicit resume must find that detour;
        // it must not silently continue the original line through the obstruction.
        Assert.NotNull((await Current(a)).BlockedAt);
        var replies=await Task.WhenAll(Resume(a,request),Resume(a,request));Assert.All(replies,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
        await AssertRuleBrokenAsync(await Resume(a,request with{BlockedAt=request.BlockedAt.AddSeconds(1)}),"travel.reusedRequest");
        DateTimeOffset resumedEnd;
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);Assert.Equal(started,c.CycleStartedAt);Assert.Equal(Desertod.Domain.Rules.RestState.RestRequested,c.RestState);
            Assert.Null(c.Trip!.BlockedAt);resumedEnd=c.CycleEndsAt!.Value;Assert.True(resumedEnd>factory.Clock.GetUtcNow());
            var world=await db.Villages.SingleAsync(w=>w.Id==c.VillageId);Assert.False(world.Fortifications.Single().Open);
            Assert.False(RegionalTravel.Reconcile(world,c.Trip,c.Trip.SetOutAt));
        }
        factory.Clock.Advance(resumedEnd-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
        var rested=await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));Assert.Equal("Resting",rested.GetProperty("restState").GetString());
        await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);Assert.True(c.CycleSettled);
            Assert.Equal(10,(await db.GameWallets.SingleAsync(w=>w.AccountId==c.AccountId&&w.VillageId==c.VillageId)).Coins);
            Assert.Equal(WorkClaimStatus.Paid,(await db.WorkOrderClaims.SingleAsync(x=>x.WorkOrderId==order)).Status);
            Assert.Equal(0,(await db.WorkOrders.SingleAsync(o=>o.Id==order)).ReservedCoins);
        }
    }

    [Fact]
    public async Task Final_commit_failure_rolls_back_both_gate_and_traveller_then_retry_stops_only_the_own_journey()
    {
        var(v,a)=await Walking();await using var db=await Db();var id=await db.Villages.Where(w=>w.Slug==v.Slug).Select(w=>w.Id).SingleAsync();
        var name="reject_travel_"+Guid.NewGuid().ToString("N");
        // Own throwaway database, generated identifiers and UUID only; the trigger fires at final transaction commit.
        await db.Database.ExecuteSqlRawAsync($"CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Id\"='{id}'::uuid THEN RAISE EXCEPTION 'Synthetic travel final commit failure'; END IF; RETURN NEW; END; $$; CREATE CONSTRAINT TRIGGER {name} AFTER UPDATE ON \"Villages\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION {name}();");
        try{Assert.Equal(HttpStatusCode.InternalServerError,(await Gate(v,false)).StatusCode);}
        finally{await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {name} ON \"Villages\"; DROP FUNCTION {name}();");}
        await using(var check=await Db()){Assert.True((await check.Villages.SingleAsync(w=>w.Id==id)).Fortifications.Single().Open);Assert.Null((await check.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip!.BlockedAt);}
        Assert.Equal(HttpStatusCode.OK,(await Gate(v,false)).StatusCode);var blocked=await Current(a);Assert.NotNull(blocked.BlockedAt);
        factory.Clock.Advance(blocked.BlockedAt!.Value-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
        var stranger=await players.JoinAsync(v,"Different walker");var request=new ResumeJourneyRequest(Guid.NewGuid(),blocked.Id,blocked.BlockedAt.Value);
        await AssertRuleBrokenAsync(await Resume(stranger,request),"travel.changed");
        var anonymous=factory.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(v.Path+"/me/journey/resume",request)).StatusCode);
        Assert.Equal(blocked.BlockedAt,(await Current(a)).BlockedAt);
    }

    [Fact]
    public async Task Old_routed_json_without_pause_fields_loads_and_can_be_stopped_after_a_reload()
    {
        var(v,a)=await Walking();
        await using(var db=await Db())await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Citizens\" SET \"Trip\"=\"Trip\"-'Id'-'RouteEndsAt'-'BlockedAt'-'BlockReason'-'LastResumeRequestId'-'LastResumeFingerprint' WHERE \"Id\"={a.Id}");
        var old=await Current(a);Assert.Equal(Guid.Empty,old.Id);Assert.Null(old.RouteEndsAt);Assert.NotEmpty(old.Waypoints);
        await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));Assert.Equal(HttpStatusCode.OK,(await Gate(v,false)).StatusCode);
        var waiting=await Current(a);Assert.NotNull(waiting.BlockedAt);Assert.Equal(old.SetOutAt,waiting.SetOutAt);
        Assert.Equal(old.To,waiting.To);Assert.Equal(old.Waypoints.Count,waiting.Waypoints.Count);
    }
}
