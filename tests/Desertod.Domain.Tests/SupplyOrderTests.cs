using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Storage;

namespace Desertod.Domain.Tests;

public sealed class SupplyOrderTests
{
    [Fact]
    public void Partial_deliveries_and_cancellation_preserve_one_finite_fund()
    {
        var order = new SupplyOrder { RequestedAmount = 7, RemainingAmount = 7, UnitPrice = 3,
            ReservedCoins = SupplyOrderRules.Budget(7, 3), ExpiresAt = TestWorld.Now.AddHours(1) };
        Assert.Equal(6, SupplyOrderRules.Deliver(order, 2, TestWorld.Now));
        Assert.Equal(2, order.DeliveredAmount);Assert.Equal(5, order.RemainingAmount);Assert.Equal(15, order.ReservedCoins);
        Assert.Equal(15, SupplyOrderRules.Close(order, SupplyOrderStatus.Cancelled));
        Assert.Equal(0, SupplyOrderRules.Close(order, SupplyOrderStatus.Expired));
        Assert.Throws<GameRuleException>(()=>SupplyOrderRules.Deliver(order, 1, TestWorld.Now));
        Assert.Equal(2, order.DeliveredAmount);Assert.Equal(0, order.ReservedCoins);
    }

    [Fact]
    public void Final_delivery_completes_without_another_refund_and_expiry_does_not_pay()
    {
        var order = new SupplyOrder { RequestedAmount=1,RemainingAmount=1,UnitPrice=9,ReservedCoins=9,ExpiresAt=TestWorld.Now.AddSeconds(1) };
        Assert.Throws<GameRuleException>(()=>SupplyOrderRules.Deliver(order,1,order.ExpiresAt));
        Assert.Equal(9, order.ReservedCoins);
        Assert.Equal(9, SupplyOrderRules.Deliver(order,1,TestWorld.Now));
        Assert.Equal(SupplyOrderStatus.Completed,order.Status);
        Assert.Equal(0,SupplyOrderRules.Close(order,SupplyOrderStatus.Cancelled));
        Assert.Throws<GameRuleException>(()=>SupplyOrderRules.Deliver(order,1,TestWorld.Now));
    }

    [Fact]
    public void Invalid_terms_or_quantity_cannot_create_unfunded_goods_or_mutate_the_order()
    {
        Assert.Equal(10_000_000_000,SupplyOrderRules.Budget(10_000,1_000_000));
        foreach(var (amount,price) in new[]{(0,1L),(10001,1L),(1,0L),(1,1000001L)})
            Assert.Throws<GameRuleException>(()=>SupplyOrderRules.Budget(amount,price));
        var order=new SupplyOrder{RemainingAmount=2,UnitPrice=3,ReservedCoins=6,ExpiresAt=TestWorld.Now.AddHours(1)};
        Assert.Throws<GameRuleException>(()=>SupplyOrderRules.Deliver(order,3,TestWorld.Now));
        Assert.Equal(2,order.RemainingAmount);Assert.Equal(6,order.ReservedCoins);
    }

    [Fact]
    public void Sale_reserve_counts_only_fresh_unsharpened_public_goods_in_usable_stores()
    {
        var village=TestWorld.NewVillage(false);village.Storages.Clear();
        var publicStore=StorageBuilding.Create(StorageKind.TownHall,"2,2",TestWorld.Now);
        publicStore.Put(ResourceKind.StoneAxe,3);publicStore.Put(ResourceKind.StoneAxe,2,durability:3);
        var privateStore=StorageBuilding.Create(StorageKind.TownHall,"6,2",TestWorld.Now);privateStore.OwnerAccountId=Guid.NewGuid();privateStore.Put(ResourceKind.StoneAxe,3);
        var ruined=StorageBuilding.Create(StorageKind.TownHall,"10,2",TestWorld.Now);ruined.Put(ResourceKind.StoneAxe,3);ruined.Damage=100;
        village.Storages.AddRange([publicStore,privateStore,ruined]);
        var economy=new KingdomEconomy();TradeReserveRules.Set(economy,ResourceKind.StoneAxe,2);
        Assert.Equal(3,TradeReserveRules.PlainPublicStock(village,ResourceKind.StoneAxe));
        Assert.Equal(1,TradeReserveRules.ForSale(village,economy,ResourceKind.StoneAxe));
        Assert.Equal(3,privateStore.Amount(ResourceKind.StoneAxe));
    }

    [Fact]
    public void Reserve_can_be_above_stock_but_does_not_block_public_work_or_create_goods()
    {
        var village=TestWorld.NewVillage(false);village.Store(ResourceKind.Wood,5);
        var economy=new KingdomEconomy();TradeReserveRules.Set(economy,ResourceKind.Wood,100);
        Assert.Equal(0,TradeReserveRules.ForSale(village,economy,ResourceKind.Wood));
        Assert.Equal(5,village.Take(ResourceKind.Wood,5));
        TradeReserveRules.Set(economy,ResourceKind.Wood,3);Assert.Single(economy.TradeReserves);
        TradeReserveRules.Set(economy,ResourceKind.Wood,0);Assert.Empty(economy.TradeReserves);
        Assert.Throws<GameRuleException>(()=>TradeReserveRules.Set(economy,ResourceKind.ResearchPoints,10));
        Assert.Throws<GameRuleException>(()=>TradeReserveRules.Set(economy,ResourceKind.Food,-1));
    }

    [Fact]
    public void Old_or_null_reserve_collection_means_no_automatic_reservation()
    {
        Assert.Empty(JsonSerializer.Deserialize<KingdomEconomy>("{}")!.TradeReserves);
        Assert.Empty(JsonSerializer.Deserialize<KingdomEconomy>("{\"TradeReserves\":null}")!.TradeReserves);
    }
}
