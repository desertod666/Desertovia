using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;

public sealed class PlayerFeedback077Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Meal_prefers_carried_food_to_personal_and_communal_food(bool privateWork)
    {
        var village = NewVillage(); var citizen = NewCitizen(village);
        citizen.IsHungry=true; citizen.CyclePrivateOutput=privateWork;
        citizen.Carry(ResourceKind.Food,2); PersonalInventory.Add(citizen,ResourceKind.Food,2,15);
        var common=village.Stock(ResourceKind.Food);
        Assert.True(FoodRules.Eat(citizen,village,Now,!privateWork));
        Assert.False(citizen.IsHungry);Assert.Equal(1,citizen.Carried(ResourceKind.Food));
        Assert.Equal(2,PersonalInventory.Amount(citizen,ResourceKind.Food));Assert.Equal(common,village.Stock(ResourceKind.Food));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Meal_can_use_personal_food_for_either_work_type_and_preserves_remaining_basis(bool privateWork)
    {
        var village=NewVillage();var citizen=NewCitizen(village);citizen.IsHungry=true;
        PersonalInventory.Add(citizen,ResourceKind.Food,2,15);var stock=village.Stock(ResourceKind.Food);
        var energy=citizen.StoredEnergy;
        Assert.True(FoodRules.Eat(citizen,village,Now,!privateWork));
        Assert.Equal(1,PersonalInventory.Amount(citizen,ResourceKind.Food));Assert.Equal(8,citizen.PersonalGoods.Single().CostBasis);
        Assert.Equal(stock,village.Stock(ResourceKind.Food));Assert.Equal(energy,citizen.StoredEnergy);
    }

    [Fact]
    public void Private_meal_cannot_spend_public_food()
    {
        var village=NewVillage();var citizen=NewCitizen(village);var stock=village.Stock(ResourceKind.Food);
        Assert.False(FoodRules.Eat(citizen,village,Now,false));Assert.True(citizen.IsHungry);
        Assert.Equal(stock,village.Stock(ResourceKind.Food));
        Assert.True(FoodRules.Eat(citizen,village,Now,true));Assert.Equal(stock-1,village.Stock(ResourceKind.Food));
    }

    [Fact]
    public void Outside_gathering_cycle_eats_its_carried_food()
    {
        var village=NewVillage();var citizen=NewCitizen(village);citizen.IsHungry=true;citizen.Carry(ResourceKind.Food,1);
        var context=WorkContext.For(village);var stock=village.Stock(ResourceKind.Food);
        // Test the meal, rather than an arbitrary tile that seeded terrain may put under water.
        var outside=RegionalTravel.WesternExit(village);Assert.NotNull(outside);
        Assert.False(KingdomRules.Area(village).Contains(outside.Value.ToTile()));
        CycleEngine.Start(citizen,Task("quarry-stone"),context,Now,outside.Value);
        Assert.True(citizen.CyclePrivateOutput);
        var report=Finish(citizen,context);
        Assert.False(report.Hungry);Assert.False(citizen.IsHungry);Assert.Equal(0,citizen.Carried(ResourceKind.Food));
        Assert.Equal(stock,village.Stock(ResourceKind.Food));
    }

    [Fact]
    public void Selected_repair_mends_ten_percent_of_only_selected_building()
    {
        var village=NewVillage();var citizen=NewCitizen(village);var context=WorkContext.For(village);
        village.Store(ResourceKind.Wood,4);village.Store(ResourceKind.Stone,2);
        var selected=village.Storages[0];selected.Damage=30;village.Storages[1].Damage=90;
        CycleEngine.Start(citizen,Task("repair"),context,Now,workplacePlotId:selected.PlotId);
        Assert.Equal(selected.PlotId,citizen.CurrentTargetPlotId);
        var report=Finish(citizen,context);
        Assert.Equal(10,report.Repaired);Assert.Equal(selected.PlotId,report.TargetPlotId);Assert.Equal(20,selected.Damage);Assert.Equal(90,village.Storages[1].Damage);
        Assert.Equal(2,village.Stock(ResourceKind.Wood));Assert.Equal(1,village.Stock(ResourceKind.Stone));
    }

    [Fact]
    public void Already_repaired_or_vanished_target_does_not_redirect_the_paid_cycle()
    {
        foreach(var vanished in new[]{false,true})
        {
            var village=NewVillage();var citizen=NewCitizen(village);var context=WorkContext.For(village);
            village.Store(ResourceKind.Wood,4);village.Store(ResourceKind.Stone,2);
            var target=village.Storages[0];target.Damage=20;var other=village.Storages[1];other.Damage=90;
            CycleEngine.Start(citizen,Task("repair"),context,Now,workplacePlotId:target.PlotId);
            if(vanished)village.Storages.Remove(target);else target.Damage=0;
            var report=Finish(citizen,context);
            Assert.Equal(0,report.Repaired);Assert.Equal(0,report.PrimaryExperience);Assert.Equal(90,other.Damage);
        }
    }

    [Fact]
    public void Private_repair_requires_owner_and_own_inputs()
    {
        var village=NewVillage();var citizen=NewCitizen(village);var context=WorkContext.For(village);
        var target=new Structure{Kind=BuildingKind.Hut,PlotId="-4,10",OwnerAccountId=citizen.AccountId,Damage=20};village.Structures.Add(target);
        village.Store(ResourceKind.Wood,10);village.Store(ResourceKind.Stone,10);
        Assert.Throws<Desertod.Domain.GameRuleException>(()=>CycleEngine.Start(citizen,Task("repair"),context,Now,workplacePlotId:target.PlotId));
        PersonalInventory.Add(citizen,ResourceKind.Wood,2,10);PersonalInventory.Add(citizen,ResourceKind.Stone,1,4);
        CycleEngine.Start(citizen,Task("repair"),context,Now,workplacePlotId:target.PlotId);
        Assert.True(citizen.CyclePrivateOutput);Assert.Equal(10,Finish(citizen,context).Repaired);Assert.Equal(10,village.Stock(ResourceKind.Wood));
        var other=NewCitizen(village);
        Assert.Equal("building.private",Assert.Throws<Desertod.Domain.GameRuleException>(()=>CycleEngine.Start(other,Task("repair"),context,Now,workplacePlotId:target.PlotId)).Code);
    }

    [Fact]
    public void Selected_unload_never_sends_goods_to_another_storage_and_keeps_overflow()
    {
        var village=NewVillage();var citizen=NewCitizen(village);
        var selected=StorageBuilding.Create(StorageKind.Heap,"2,2",Now);village.Storages.Add(selected);
        selected.Put(ResourceKind.Wood,selected.FreeSpace(ResourceKind.Wood)-2);citizen.Carry(ResourceKind.Wood,5);
        var other=village.Storages[0].Amount(ResourceKind.Wood);
        var context=WorkContext.For(village);CycleEngine.Start(citizen,Task(GameCatalog.CarryToStorage),context,Now,workplacePlotId:selected.PlotId);
        var report=Finish(citizen,context);
        Assert.Equal(2,report.Produced.Single().Amount);Assert.Equal(3,citizen.Carried(ResourceKind.Wood));Assert.Equal(other,village.Storages[0].Amount(ResourceKind.Wood));
    }

    [Fact]
    public void Storage_removed_or_filled_during_walk_retains_the_entire_load()
    {
        foreach(var removed in new[]{false,true})
        {
            var village=NewVillage();var citizen=NewCitizen(village);var selected=village.Storages[0];citizen.Carry(ResourceKind.Wood,4);
            var context=WorkContext.For(village);CycleEngine.Start(citizen,Task(GameCatalog.CarryToStorage),context,Now,workplacePlotId:selected.PlotId);
            if(removed)village.Storages.Remove(selected);else selected.Put(ResourceKind.Wood,selected.FreeSpace(ResourceKind.Wood));
            Assert.Empty(Finish(citizen,context).Produced);Assert.Equal(4,citizen.Carried(ResourceKind.Wood));
        }
    }

    [Fact]
    public void Private_warehouse_cannot_accept_public_carry_jobs_without_basis_accounting()
    {
        var village=NewVillage();var citizen=NewCitizen(village);citizen.Carry(ResourceKind.Wood,4);
        village.Storages[0].OwnerAccountId=citizen.AccountId;
        Assert.Throws<Desertod.Domain.GameRuleException>(()=>CycleEngine.Start(citizen,Task(GameCatalog.CarryToStorage),WorkContext.For(village),Now,workplacePlotId:village.Storages[0].PlotId));
        Assert.Equal(4,citizen.Carried(ResourceKind.Wood));Assert.Empty(village.Storages[0].PersonalCosts);
    }

    [Fact]
    public void Building_numbers_remain_stable_after_upgrade_demolition_and_new_construction()
    {
        var village=NewVillage();var home=new Structure{Kind=BuildingKind.Longhouse,PlotId="1,1",BuiltAt=Now};village.Structures.Add(home);
        BuildingNumbers.Ensure(village);var number=home.Number;var sequence=village.BuildingSequence;
        var site=new ConstructionSite{Kind=home.Kind,PlotId=home.PlotId,PlacedAt=Now.AddDays(1),TargetLevel=2};village.Sites.Add(site);
        BuildingNumbers.Ensure(village);Assert.Equal(number,site.Number);
        village.Sites.Remove(site);home.Level=2;village.Storages.RemoveAt(0);
        var next=new ConstructionSite{Kind=BuildingKind.Hut,PlotId="5,1",PlacedAt=Now.AddDays(2)};village.Sites.Add(next);
        BuildingNumbers.Ensure(village);Assert.Equal(sequence+1,next.Number);Assert.Equal(number,home.Number);
        BuildingNumbers.Ensure(village);Assert.Equal(sequence+1,village.BuildingSequence);
    }
    [Theory]
    [InlineData(BuildingKind.Hut)]
    [InlineData(BuildingKind.Heap)]
    public void Completed_structure_and_storage_keep_the_number_and_owner_of_their_site(BuildingKind kind)
    {
        var village=NewVillage();var owner=NewCitizen(village);
        var site=new ConstructionSite{Kind=kind,PlotId="-4,10",OwnerAccountId=owner.AccountId,PlacedAt=Now};village.Sites.Add(site);
        BuildingNumbers.Ensure(village);var number=site.Number;site.Stage=site.Definition.Stages.Count;
        Assert.Equal(kind,ConstructionRules.Advance(village,site,1,Now.AddMinutes(1)));
        Assert.DoesNotContain(site,village.Sites);
        if(kind==BuildingKind.Hut){var built=village.Structures.Single(s=>s.PlotId==site.PlotId);Assert.Equal(number,built.Number);Assert.Equal(owner.AccountId,built.OwnerAccountId);}
        else {var built=village.Storages.Single(s=>s.PlotId==site.PlotId);Assert.Equal(number,built.Number);Assert.Equal(owner.AccountId,built.OwnerAccountId);}
    }

}
