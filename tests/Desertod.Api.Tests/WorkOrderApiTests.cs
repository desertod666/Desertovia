using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class WorkOrderApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
    private static Task<EconomyView?> View(HttpClient client,FoundedVillage village)=>client.GetFromJsonAsync<EconomyView>(village.Path+"/economy",JsonOptions);
    private static Task<HttpResponseMessage> Action(HttpClient client,FoundedVillage v,string action,Guid? target=null)=>client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action,target});
    private async Task<(FoundedVillage V,Resident A,Resident B,Guid Order)> Setup(int cycles=1)
    {
        var v=await players.FoundVillageAsync("Funded work"); var a=await players.JoinAsync(v,"First");var b=await players.JoinAsync(v,"Second");
        await View(v.Tsar,v); await View(a.Client,v); await View(b.Client,v);
        await using(var db=await Db())
        {
            var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
            (await db.KingdomEconomies.SingleAsync(e=>e.VillageId==id)).TreasuryCoins=100;
            await db.SaveChangesAsync();
        }
        await OkJsonAsync(await Action(v.Tsar,v,"enable-work-contracts"));
        var order=Guid.NewGuid();
        var publish=new{requestId=order,action="publish-work-order",taskId="chop-wood",cycles,pay=10,expiresAt=factory.Clock.GetUtcNow().AddHours(1)};
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",publish));
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",publish));
        var view=(await View(v.Tsar,v))!;
        Assert.Equal(100-cycles*10,view.Treasury);
        return(v,a,b,Assert.Single(view.WorkOrders).Id);
    }
    private static Task<HttpResponseMessage> Start(Resident worker,Guid order,Guid requestId)=>worker.Client.PostAsJsonAsync(worker.Path+"/me/task",new{taskId="chop-wood",workOrderId=order,requestId});

    [Fact]
    public async Task Last_slot_is_atomic_and_finished_work_pays_once_after_cancellation_and_expiry()
    {
        var(v,a,b,order)=await Setup(2);
        var request=Guid.NewGuid(); var started=await OkJsonAsync(await Start(a,order,request));
        Assert.Equal("PublicContract",started.GetProperty("cycleOwnership").GetString());
        await OkJsonAsync(await Action(v.Tsar,v,"cancel-work-order",order));
        Assert.Equal(90,(await View(v.Tsar,v))!.Treasury);
        await AssertRuleBrokenAsync(await Start(b,order,Guid.NewGuid()),"workOrder.closed");
        await OkJsonAsync(await a.Client.PutAsJsonAsync(v.Path+"/me/rest",new{resting=true}));
        factory.Clock.Advance(TimeSpan.FromHours(2));
        await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        var after=await View(a.Client,v); Assert.Equal(10,after!.Coins); Assert.Equal(10,after.EarnedToday);
        for(var i=0;i<2;i++)await OkJsonAsync(await Start(a,order,request)); // lost-response retry must not accept another cycle, even while resting.
        await using var db=await Db(); var claim=await db.WorkOrderClaims.SingleAsync(c=>c.WorkOrderId==order);
        Assert.Equal(WorkClaimStatus.Paid,claim.Status); Assert.Equal(0,(await db.WorkOrders.SingleAsync(o=>o.Id==order)).ReservedCoins);
        Assert.Equal(0,await db.LedgerEntries.Where(e=>e.OperationId==claim.Id && e.Asset=="DZK").SumAsync(e=>e.Delta));
        Assert.Equal(10,(await View(a.Client,v))!.Coins);
    }

    [Fact]
    public async Task Two_workers_cannot_reserve_the_same_final_cycle_and_cancel_reopens_it()
    {
        var(v,a,b,order)=await Setup();
        var replies=await Task.WhenAll(Start(a,order,Guid.NewGuid()),Start(b,order,Guid.NewGuid()));
        Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.BadRequest);
        var winner=replies[0].IsSuccessStatusCode?a:b;
        await OkJsonAsync(await winner.Client.DeleteAsync(v.Path+"/me/task"));
        var view=await View(v.Tsar,v);Assert.Equal(1,Assert.Single(view!.WorkOrders).AvailableCycles);Assert.Equal(10,view.WorkOrders[0].ReservedCoins);
        Assert.Equal(0,(await View(winner.Client,v))!.Coins);
        factory.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(100,(await View(v.Tsar,v))!.Treasury);
        Assert.Equal(100,(await View(v.Tsar,v))!.Treasury);
    }

    [Fact]
    public async Task No_fund_or_wrong_actor_or_other_kingdom_cannot_create_or_accept_a_promise()
    {
        var(v,a,b,order)=await Setup();
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="publish-work-order",taskId="chop-wood",cycles=100,pay=10,expiresAt=factory.Clock.GetUtcNow().AddHours(1)}),"economy.funds");
        Assert.Equal(HttpStatusCode.Forbidden,(await Action(a.Client,v,"cancel-work-order",order)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Action(a.Client,v,"enable-work-contracts")).StatusCode);
        var other=await players.FoundVillageAsync("Other work");var stranger=await players.JoinAsync(other);
        await AssertRuleBrokenAsync(await Start(stranger,order,Guid.NewGuid()),"workOrder.closed");
        var accepted=Guid.NewGuid();await OkJsonAsync(await Start(a,order,accepted));
        await AssertRuleBrokenAsync(await Start(b,order,accepted),"economy.reusedRequest");
    }

    [Fact]
    public async Task Receipt_from_before_work_order_fields_can_still_be_retried_without_repeating_the_purchase()
    {
        var v=await players.FoundVillageAsync("Old receipt compatibility");var a=await players.JoinAsync(v);await View(a.Client,v);
        var request=new EconomyRequest(Guid.NewGuid(),"buy-stock",Amount:1,Price:2);
        var legacy=new{request.RequestId,request.Action,request.Resource,request.Amount,request.Price,request.Target,request.X,request.Y,request.Building};
        var fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(legacy)));
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);
            db.EconomyReceipts.Add(new(){VillageId=c.VillageId,AccountId=c.AccountId,RequestId=request.RequestId,Fingerprint=fingerprint,At=factory.Clock.GetUtcNow()});
            (await db.GameWallets.SingleAsync(w=>w.VillageId==c.VillageId&&w.AccountId==c.AccountId)).Coins=121;
            await db.SaveChangesAsync();
        }
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        Assert.Equal(121,(await View(a.Client,v))!.Coins);
    }

    [Fact]
    public async Task Earned_pay_waits_in_reserve_when_wallet_full_and_can_be_collected_after_spending()
    {
        var(v,a,_,order)=await Setup();await OkJsonAsync(await Start(a,order,Guid.NewGuid()));
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);
            (await db.GameWallets.SingleAsync(w=>w.VillageId==c.VillageId&&w.AccountId==c.AccountId)).Coins=1_000_000_000_000;
            await db.SaveChangesAsync();
        }
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        var full=await View(a.Client,v);Assert.Equal(10,full!.WorkPayAwaitingRoom);Assert.Equal(10,Assert.Single(full.WorkOrders).ReservedCoins);
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="fund-treasury",price=10}));
        var request=new{requestId=Guid.NewGuid(),action="claim-work-pay"};
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        var paid=await View(a.Client,v);Assert.Equal(1_000_000_000_000,paid!.Coins);Assert.Equal(0,paid.WorkPayAwaitingRoom);Assert.Equal(10,paid.EarnedToday);
    }

    [Fact]
    public async Task Leaving_world_returns_the_unfinished_cycle_instead_of_stranding_its_reserve()
    {
        var(v,a,_,order)=await Setup();await OkJsonAsync(await Start(a,order,Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NoContent,(await a.Client.DeleteAsync(v.Path+"/me")).StatusCode);
        var view=await View(v.Tsar,v);Assert.Equal(1,Assert.Single(view!.WorkOrders).AvailableCycles);
        await OkJsonAsync(await Action(v.Tsar,v,"cancel-work-order",order));Assert.Equal(100,(await View(v.Tsar,v))!.Treasury);
    }

    [Fact]
    public async Task Enrolling_does_not_rewrite_accepted_legacy_pay_but_new_uncontracted_work_is_voluntary()
    {
        var v=await players.FoundVillageAsync("Transition promises"); var a=await players.JoinAsync(v);
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood"}));
        await OkJsonAsync(await Action(v.Tsar,v,"enable-work-contracts"));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        Assert.Equal(10,(await View(a.Client,v))!.Coins);
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood"}));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        Assert.Equal(10,(await View(a.Client,v))!.Coins);
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood",ownership="PrivateExtraction"}));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        Assert.True((await players.LoadCitizenAsync(a.Id)).PersonalGoods.Count>0);
        Assert.Equal(10,(await View(a.Client,v))!.Coins);
    }
}
