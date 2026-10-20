using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SupplyOrderApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static async Task<EconomyView> View(HttpClient client,FoundedVillage v)=>(await client.GetFromJsonAsync<EconomyView>(v.Path+"/economy",Options))!;
    private static Task<HttpResponseMessage> Post(HttpClient client,FoundedVillage v,EconomyRequest body)=>client.PostAsJsonAsync(v.Path+"/economy",body,Options);
    private static EconomyRequest Request(string action,int amount=1,long price=0,Guid? target=null)=>new(Guid.NewGuid(),action,Amount:amount,Price:price,Target:target);

    private async Task<(FoundedVillage V,Resident A,Resident B,Guid Id)> Setup(bool known=true)
    {
        var v=await players.FoundVillageAsync("Backed supplies");var a=await players.JoinAsync(v,"Supplier");var b=await players.JoinAsync(v,"Other supplier");
        await View(a.Client,v);await View(b.Client,v);
        await using var db=await Db();var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        village.Storages.Add(StorageBuilding.Create(StorageKind.TownHall,"3,3",factory.Clock.GetUtcNow()));
        village.Store(ResourceKind.Wood,10);
        var ca=await db.Citizens.SingleAsync(c=>c.Id==a.Id);var cb=await db.Citizens.SingleAsync(c=>c.Id==b.Id);
        PersonalInventory.Add(ca,ResourceKind.Wood,5,7,known);PersonalInventory.Add(cb,ResourceKind.Wood,5,15);
        (await db.KingdomEconomies.SingleAsync(e=>e.VillageId==village.Id)).TreasuryCoins=100;
        foreach(var wallet in await db.GameWallets.Where(w=>w.VillageId==village.Id).ToArrayAsync())wallet.Coins=100;
        await db.SaveChangesAsync();return(v,a,b,village.Id);
    }
    private async Task<EconomyRequest> Publish(FoundedVillage v,int amount=4,long price=3)
    {
        var request=Request("publish-supply-order",amount,price) with {ExpiresAt=factory.Clock.GetUtcNow().AddHours(1)};
        await OkJsonAsync(await Post(v.Tsar,v,request));return request;
    }
    private static EconomyRequest Delivery(EconomyRequest order,int amount)=>Request("fulfill-supply-order",amount,order.Price,order.RequestId);

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Partial_delivery_transfers_actual_goods_cost_and_reserved_money_once(bool known)
    {
        var(v,a,b,id)=await Setup(known);var order=await Publish(v);await OkJsonAsync(await Post(v.Tsar,v,order));
        Assert.Equal(88,(await View(v.Tsar,v)).Treasury);
        var delivery=Delivery(order,2);
        await OkJsonAsync(await Post(a.Client,v,delivery));await OkJsonAsync(await Post(a.Client,v,delivery));
        var view=await View(a.Client,v);Assert.Equal(106,view.Coins);Assert.Equal(known?4:0,view.ProfitToday);
        Assert.Equal(2,Assert.Single(view.SupplyOrders).RemainingAmount);Assert.Equal(6,view.SupplyOrders[0].ReservedCoins);
        var proof=Assert.Single(view.MySupplyDeliveries);Assert.Equal(2,proof.Amount);Assert.Equal(2,proof.CostBasis);Assert.Equal(known,proof.CostBasisKnown);
        Assert.Empty((await View(b.Client,v)).MySupplyDeliveries);
        var inventory=Assert.Single(view.Inventory);Assert.Equal(3,inventory.Amount);Assert.Equal(5,inventory.CostBasis);
        await using var db=await Db();Assert.Equal(12,(await db.Villages.SingleAsync(x=>x.Id==id)).Stock(ResourceKind.Wood));
        Assert.Single(await db.SupplyDeliveries.Where(d=>d.SupplyOrderId==order.RequestId).ToArrayAsync());
        Assert.Equal(0,await db.LedgerEntries.Where(e=>e.OperationId==delivery.RequestId&&e.Asset=="DZK").SumAsync(e=>e.Delta));
        await AssertRuleBrokenAsync(await Post(b.Client,v,delivery),"economy.reusedRequest");
        await AssertRuleBrokenAsync(await Post(v.Tsar,v,order with {ExpiresAt=order.ExpiresAt!.Value.AddMinutes(1)}),"economy.reusedRequest");
    }

    [Fact]
    public async Task Last_quantity_is_atomic_between_two_suppliers()
    {
        var(v,a,b,id)=await Setup();var order=await Publish(v,2);
        var results=await Task.WhenAll(Post(a.Client,v,Delivery(order,2)),Post(b.Client,v,Delivery(order,2)));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.BadRequest);
        await using var db=await Db();var saved=await db.SupplyOrders.SingleAsync(o=>o.Id==order.RequestId);
        Assert.Equal(SupplyOrderStatus.Completed,saved.Status);Assert.Equal(0,saved.ReservedCoins);Assert.Equal(2,saved.DeliveredAmount);
        Assert.Equal(12,(await db.Villages.SingleAsync(x=>x.Id==id)).Stock(ResourceKind.Wood));
        Assert.Equal(206,(await View(a.Client,v)).Coins+(await View(b.Client,v)).Coins);
    }

    [Fact]
    public async Task Expiry_and_repeated_close_refund_only_the_undelivered_fund()
    {
        var(v,a,_,id)=await Setup();var order=await Publish(v);await OkJsonAsync(await Post(a.Client,v,Delivery(order,1)));
        factory.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(97,(await View(v.Tsar,v)).Treasury);
        Assert.Equal(97,(await View(v.Tsar,v)).Treasury);
        await OkJsonAsync(await Post(v.Tsar,v,Request("cancel-supply-order",target:order.RequestId)));
        await AssertRuleBrokenAsync(await Post(a.Client,v,Delivery(order,1)),"supplyOrder.closed");
        await using var db=await Db();Assert.Equal(SupplyOrderStatus.Expired,(await db.SupplyOrders.SingleAsync(o=>o.Id==order.RequestId)).Status);
        Assert.Equal(0,await db.LedgerEntries.Where(e=>e.VillageId==id&&e.Asset=="DZK").SumAsync(e=>e.Delta));
    }

    [Fact]
    public async Task Failed_storage_or_wallet_credit_rolls_back_goods_order_and_payment()
    {
        var(v,a,_,id)=await Setup();var order=await Publish(v);
        await players.ArrangeVillageAsync(v.Slug,(village,now)=>{foreach(var storage in village.Storages){storage.Damage=100;storage.RuinedAt=now;}});
        var delivery=Delivery(order,2);
        await AssertRuleBrokenAsync(await Post(a.Client,v,delivery),"storage.targetFull");
        await using(var db=await Db())
        {
            var saved=await db.SupplyOrders.SingleAsync(o=>o.Id==order.RequestId);Assert.Equal(4,saved.RemainingAmount);Assert.Equal(12,saved.ReservedCoins);
            Assert.Empty(await db.SupplyDeliveries.Where(d=>d.SupplyOrderId==order.RequestId).ToArrayAsync());
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==a.Id);Assert.Equal(5,PersonalInventory.Amount(citizen,ResourceKind.Wood));
            var wallet=await db.GameWallets.SingleAsync(w=>w.VillageId==id&&w.AccountId==citizen.AccountId);wallet.Coins=EconomyRules.MaximumBalance;
            await db.SaveChangesAsync();
        }
        await players.ArrangeVillageAsync(v.Slug,(village,_)=>{foreach(var storage in village.Storages){storage.Damage=0;storage.RuinedAt=null;}});
        await AssertRuleBrokenAsync(await Post(a.Client,v,delivery),"economy.limit");
        await using(var db=await Db())
        {
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==a.Id);Assert.Equal(5,PersonalInventory.Amount(citizen,ResourceKind.Wood));
            var wallet=await db.GameWallets.SingleAsync(w=>w.VillageId==id&&w.AccountId==citizen.AccountId);wallet.Coins=100;await db.SaveChangesAsync();
        }
        await OkJsonAsync(await Post(a.Client,v,delivery));Assert.Equal(106,(await View(a.Client,v)).Coins);
    }

    [Fact]
    public async Task Authority_and_world_boundaries_reject_unfunded_or_forged_orders()
    {
        var(v,a,_,_)=await Setup();
        var publication=Request("publish-supply-order",100,100) with {ExpiresAt=factory.Clock.GetUtcNow().AddHours(1)};
        await AssertRuleBrokenAsync(await Post(v.Tsar,v,publication),"economy.funds");
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(a.Client,v,publication)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(a.Client,v,Request("set-trade-reserve",2))).StatusCode);
        var order=await Publish(v);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(a.Client,v,Request("cancel-supply-order",target:order.RequestId))).StatusCode);
        var other=await players.FoundVillageAsync("Other supply world");var stranger=await players.JoinAsync(other);
        await AssertRuleBrokenAsync(await Post(stranger.Client,other,Delivery(order,1)),"supplyOrder.closed");
        await AssertRuleBrokenAsync(await Post(a.Client,v,Delivery(order,1) with {Price=4}),"economy.priceChanged");
    }

    [Fact]
    public async Task Public_sales_honor_selected_reserve_and_do_not_feed_active_purchase_orders()
    {
        var(v,a,_,id)=await Setup();
        await OkJsonAsync(await Post(v.Tsar,v,Request("set-trade-reserve",8)));
        Assert.Equal(2,(await View(a.Client,v)).StockForSale[ResourceKind.Wood]);
        await AssertRuleBrokenAsync(await Post(a.Client,v,Request("buy-stock",3,2)),"market.reserve");
        await OkJsonAsync(await Post(a.Client,v,Request("buy-stock",2,2)));
        await AssertRuleBrokenAsync(await Post(a.Client,v,Request("buy-stock",1,2)),"market.reserve");
        await OkJsonAsync(await Post(v.Tsar,v,Request("set-trade-reserve",2) with {Resource=ResourceKind.Stone}));
        await OkJsonAsync(await Post(v.Tsar,v,Request("set-trade-reserve",0)));
        Assert.Equal(ResourceKind.Stone,Assert.Single((await View(a.Client,v)).TradeReserves).Resource);
        var order=await Publish(v,1);
        Assert.Equal(0,(await View(a.Client,v)).StockForSale[ResourceKind.Wood]);
        await AssertRuleBrokenAsync(await Post(a.Client,v,Request("buy-stock",1,2)),"supplyOrder.publicSaleBlocked");
        await OkJsonAsync(await Post(v.Tsar,v,Request("cancel-supply-order",target:order.RequestId)));
        Assert.Equal(8,(await View(a.Client,v)).StockForSale[ResourceKind.Wood]);
        await using var db=await Db();Assert.Equal(8,(await db.Villages.SingleAsync(x=>x.Id==id)).Stock(ResourceKind.Wood));
    }

    [Fact]
    public async Task Simultaneous_cancellation_and_delivery_conserve_treasury_wallets_and_stock()
    {
        var(v,a,_,id)=await Setup();var order=await Publish(v,2);
        var responses=await Task.WhenAll(Post(v.Tsar,v,Request("cancel-supply-order",target:order.RequestId)),Post(a.Client,v,Delivery(order,2)));
        Assert.Equal(HttpStatusCode.OK,responses[0].StatusCode);
        var view=await View(a.Client,v);
        await using var db=await Db();var saved=await db.SupplyOrders.SingleAsync(o=>o.Id==order.RequestId);
        Assert.Equal(0,saved.ReservedCoins);
        Assert.Equal(200,view.Treasury+view.Coins);
        var actualStock=(await db.Villages.SingleAsync(x=>x.Id==id)).Stock(ResourceKind.Wood);
        Assert.Equal(15,actualStock+Assert.Single(view.Inventory).Amount);
        Assert.Equal(responses[1].IsSuccessStatusCode?2:0,saved.DeliveredAmount);
    }

    [Fact]
    public async Task Failure_at_final_commit_leaves_supply_goods_fund_receipt_and_profit_retryable()
    {
        var(v,a,_,id)=await Setup();var order=await Publish(v);var delivery=Delivery(order,2);
        await using var db=await Db();var trigger=await EconomyCommitFailure.Install(db,delivery.RequestId);
        try { Assert.Equal(HttpStatusCode.InternalServerError,(await Post(a.Client,v,delivery)).StatusCode); }
        finally { await EconomyCommitFailure.Remove(db,trigger); }
        var unchanged=await View(a.Client,v);Assert.Equal(100,unchanged.Coins);Assert.Equal(0,unchanged.ProfitToday);
        Assert.Equal(5,Assert.Single(unchanged.Inventory).Amount);Assert.Equal(12,Assert.Single(unchanged.SupplyOrders).ReservedCoins);
        Assert.False(await db.EconomyReceipts.AnyAsync(r=>r.VillageId==id&&r.RequestId==delivery.RequestId));
        Assert.False(await db.SupplyDeliveries.AnyAsync(r=>r.Id==delivery.RequestId));
        await OkJsonAsync(await Post(a.Client,v,delivery));await OkJsonAsync(await Post(a.Client,v,delivery));
        Assert.Equal(106,(await View(a.Client,v)).Coins);
        Assert.Equal(12,(await db.Villages.SingleAsync(v=>v.Id==id)).Stock(ResourceKind.Wood));
    }

    [Fact]
    public async Task Two_market_buyers_can_retry_after_actual_commit_failure_without_duplicate_goods()
    {
        var(v,seller,buyer,id)=await Setup();var other=await players.JoinAsync(v,"Second market buyer");await View(other.Client,v);
        await using var db=await Db();var person=await db.Citizens.SingleAsync(c=>c.Id==other.Id);
        (await db.GameWallets.SingleAsync(w=>w.VillageId==id&&w.AccountId==person.AccountId)).Coins=100;await db.SaveChangesAsync();
        var lot=Request("list",2,10);await OkJsonAsync(await Post(seller.Client,v,lot));
        var first=Request("buy-lot",target:lot.RequestId);var second=Request("buy-lot",target:lot.RequestId);
        var trigger=await EconomyCommitFailure.Install(db,first.RequestId,second.RequestId);
        try
        {
            var failed=await Task.WhenAll(Post(buyer.Client,v,first),Post(other.Client,v,second));
            Assert.All(failed,r=>Assert.Equal(HttpStatusCode.InternalServerError,r.StatusCode));
        }
        finally { await EconomyCommitFailure.Remove(db,trigger); }
        Assert.Equal("open",(await db.MarketLots.AsNoTracking().SingleAsync(l=>l.Id==lot.RequestId)).Status);
        Assert.Equal(100,(await View(buyer.Client,v)).Coins);Assert.Equal(100,(await View(other.Client,v)).Coins);
        var results=await Task.WhenAll(Post(buyer.Client,v,first),Post(other.Client,v,second));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.BadRequest);
        var winner=results[0].IsSuccessStatusCode?buyer:other;var request=results[0].IsSuccessStatusCode?first:second;
        await OkJsonAsync(await Post(winner.Client,v,request));
        Assert.Equal(300,(await View(seller.Client,v)).Coins+(await View(buyer.Client,v)).Coins+(await View(other.Client,v)).Coins);
        Assert.Equal("sold",(await db.MarketLots.AsNoTracking().SingleAsync(l=>l.Id==lot.RequestId)).Status);
        Assert.Equal(10,(await View(seller.Client,v)).Inventory.Sum(g=>g.Amount)+(await View(buyer.Client,v)).Inventory.Sum(g=>g.Amount)+(await View(other.Client,v)).Inventory.Sum(g=>g.Amount));
    }

    [Fact]
    public async Task Expired_work_and_supply_funds_can_fund_each_other_without_a_menu_refresh()
    {
        var(v,_,_,id)=await Setup();
        await OkJsonAsync(await Post(v.Tsar,v,Request("enable-work-contracts")));
        var work=Request("publish-work-order") with {TaskId="chop-wood",Cycles=10,Pay=10,ExpiresAt=factory.Clock.GetUtcNow().AddHours(1)};
        await OkJsonAsync(await Post(v.Tsar,v,work));
        factory.Clock.Advance(TimeSpan.FromHours(2));
        // No GET between expiration and publication: the command must release the other fund itself.
        await Publish(v,100,1);
        factory.Clock.Advance(TimeSpan.FromHours(2));
        await OkJsonAsync(await Post(v.Tsar,v,work with {RequestId=Guid.NewGuid(),ExpiresAt=factory.Clock.GetUtcNow().AddHours(1)}));
        await using var db=await Db();
        Assert.Equal(0,(await db.KingdomEconomies.SingleAsync(e=>e.VillageId==id)).TreasuryCoins);
        Assert.Equal(100,await db.WorkOrders.Where(o=>o.VillageId==id).SumAsync(o=>o.ReservedCoins));
        Assert.Equal(0,await db.SupplyOrders.Where(o=>o.VillageId==id).SumAsync(o=>o.ReservedCoins));
        Assert.Equal(0,await db.LedgerEntries.Where(e=>e.VillageId==id&&e.Asset=="DZK").SumAsync(e=>e.Delta));
    }
}
