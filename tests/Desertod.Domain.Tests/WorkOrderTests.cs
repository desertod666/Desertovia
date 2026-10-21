using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Rules;
using Desertod.Domain.Buildings;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class WorkOrderTests
{
    private static WorkOrder Order(int cycles=2) => new() { VillageId=Guid.NewGuid(), IssuerAccountId=Guid.NewGuid(), TaskId="chop-wood",
        PayPerCycle=10,AvailableCycles=cycles,ReservedCoins=cycles*10,CreatedAt=Now,ExpiresAt=Now.AddHours(1) };
    private static WorkOrderClaim Claim(WorkOrder order) => new() { VillageId=order.VillageId,WorkOrderId=order.Id,AccountId=Guid.NewGuid(),PromisedPay=order.PayPerCycle };

    [Fact]
    public void Accepted_work_is_paid_once_after_order_cancellation_and_restart()
    {
        var order=Order(); WorkOrderRules.Accept(order,Now); var claim=Claim(order);
        Assert.Equal(10,WorkOrderRules.Close(order,WorkOrderStatus.Cancelled));
        order=JsonSerializer.Deserialize<WorkOrder>(JsonSerializer.Serialize(order))!;
        claim=JsonSerializer.Deserialize<WorkOrderClaim>(JsonSerializer.Serialize(claim))!;
        Assert.Equal(0,WorkOrderRules.Finish(order,claim,true,false,Now.AddHours(2)));
        var wallet=new GameWallet{VillageId=order.VillageId,AccountId=claim.AccountId};
        Assert.True(WorkOrderRules.Pay(order,claim,wallet));
        Assert.False(WorkOrderRules.Pay(order,claim,wallet));
        Assert.Equal(10,wallet.Coins); Assert.Equal(10,wallet.EarnedToday); Assert.Equal(0,order.ReservedCoins);
        Assert.Equal(0,WorkOrderRules.Close(order,WorkOrderStatus.Cancelled));
        Assert.Equal(0,WorkOrderRules.Finish(order,claim,true,false,Now.AddHours(3)));
    }

    [Fact]
    public void Unproductive_and_cancelled_work_returns_exact_unused_commitment()
    {
        var order=Order(1); WorkOrderRules.Accept(order,Now); var claim=Claim(order);
        Assert.Equal(0,WorkOrderRules.Finish(order,claim,false,false,Now));
        Assert.Equal(1,order.AvailableCycles); Assert.Equal(10,order.ReservedCoins);
        WorkOrderRules.Accept(order,Now); claim=Claim(order);
        Assert.Equal(0,WorkOrderRules.Close(order,WorkOrderStatus.Expired));
        Assert.Equal(10,WorkOrderRules.Finish(order,claim,false,true,Now.AddHours(2)));
        Assert.Equal(0,order.ReservedCoins); Assert.Equal(0,order.AcceptedCycles);
        Assert.Equal(0,WorkOrderRules.Finish(order,claim,false,true,Now.AddHours(3)));
    }

    [Fact]
    public void Full_wallet_keeps_earned_money_in_escrow_until_there_is_room()
    {
        var order=Order(1); WorkOrderRules.Accept(order,Now); var claim=Claim(order);
        WorkOrderRules.Finish(order,claim,true,false,Now);
        var wallet=new GameWallet{VillageId=order.VillageId,AccountId=claim.AccountId,Coins=EconomyRules.MaximumBalance};
        Assert.False(WorkOrderRules.Pay(order,claim,wallet)); Assert.Equal(10,order.ReservedCoins);
        EconomyRules.Spend(wallet,10);
        Assert.True(WorkOrderRules.Pay(order,claim,wallet)); Assert.Equal(EconomyRules.MaximumBalance,wallet.Coins);
        Assert.Equal(0,order.ReservedCoins);
    }

    [Fact]
    public void Explicit_personal_extraction_inside_town_never_takes_communal_output()
    {
        var village=NewVillage(); var citizen=NewCitizen(village); var context=WorkContext.For(village);
        CycleEngine.Start(citizen,GameCatalog.FindTask("chop-wood")!,context,Now,ownership:WorkOwnership.PrivateExtraction);
        Assert.True(citizen.CyclePrivateOutput); Assert.Equal(WorkOwnership.PrivateExtraction,citizen.CycleOwnership);
        var before=village.Stock(ResourceKind.Wood); Finish(citizen,context);
        Assert.Equal(before,village.Stock(ResourceKind.Wood)); Assert.True(PersonalInventory.Amount(citizen,ResourceKind.Wood)>0);
    }

    [Fact]
    public void Explicit_extraction_cannot_be_mislabelled_as_personal_craft()
    {
        var village=NewVillage(); var citizen=NewCitizen(village);
        var ex=Assert.Throws<Desertod.Domain.GameRuleException>(()=>CycleEngine.Start(citizen,GameCatalog.FindTask("chop-wood")!,WorkContext.For(village),Now,ownership:WorkOwnership.PrivateCraft));
        Assert.Equal("workOrder.ownership",ex.Code);
    }

    [Fact]
    public void Personal_craft_uses_only_own_materials_and_workshop_and_public_mode_cannot_take_it()
    {
        var village=NewVillage();
        village.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");
        village.Wilds=Desertod.Domain.World.Wilderness.Generate(village.Id);
        var c=NewCitizen(village,(Stat.Dexterity,4),(Stat.Strength,2));
        village.Structures.Add(new(){Kind=BuildingKind.Workshop,PlotId="-4,10",OwnerAccountId=c.AccountId,BuiltAt=Now});
        village.Research.Add(new(){TechnologyId=TechnologyCatalog.Carpentry,ResearchedAt=Now});
        PersonalInventory.Add(c,ResourceKind.Wood,7,101);
        var context=WorkContext.For(village);
        Assert.Throws<Desertod.Domain.GameRuleException>(()=>CycleEngine.Start(c,Task("saw-planks"),context,Now,workplacePlotId:"-4,10",ownership:WorkOwnership.PublicService));
        Assert.Equal(7,PersonalInventory.Amount(c,ResourceKind.Wood));
        CycleEngine.Start(c,Task("saw-planks"),context,Now,workplacePlotId:"-4,10",ownership:WorkOwnership.PrivateCraft);
        Assert.Equal(WorkOwnership.PrivateCraft,c.CycleOwnership);Assert.Equal(28,c.CycleInputCostBasis);
        Finish(c,context);Assert.Equal(28,c.PersonalGoods.Single(g=>g.Kind==ResourceKind.Planks).CostBasis);
    }
}
