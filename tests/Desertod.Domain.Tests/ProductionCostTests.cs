using System.Text.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class ProductionCostTests
{
    [Theory]
    [InlineData(0L)][InlineData(1L)][InlineData(7L)][InlineData(long.MaxValue)]
    public void Multi_output_allocation_conserves_every_unit_and_is_independent_of_output_order(long basis)
    {
        ResourceAmount[] outputs = [new(ResourceKind.Wood, 1), new(ResourceKind.Stone, 1), new(ResourceKind.Grain, 2)];
        var allocated = ProductionCost.Allocate(basis, outputs);
        var reversed = ProductionCost.Allocate(basis, outputs.Reverse().ToArray());
        Assert.Equal(basis, allocated.Values.Sum());
        foreach (var kind in allocated.Keys) Assert.Equal(allocated[kind], reversed[kind]);
        Assert.All(allocated.Values, value => Assert.InRange(value, 0, basis));
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public void Private_craft_moves_consumed_cost_into_output_and_preserves_unknown_history(bool known)
    {
        var v = NewVillage(); var c = NewCitizen(v, (Stat.Dexterity, 4), (Stat.Strength, 2));
        v.Structures.Add(new() {Kind = BuildingKind.Workshop, PlotId = "-4,10", OwnerAccountId = c.AccountId, BuiltAt = Now});
        v.Research.Add(new() {TechnologyId = TechnologyCatalog.Carpentry, ResearchedAt = Now});
        PersonalInventory.Add(c, ResourceKind.Wood, 7, 101, known);
        var context = WorkContext.For(v);
        CycleEngine.Start(c, Task("saw-planks"), context, Now, workplacePlotId: "-4,10");
        Assert.True(c.CyclePrivateOutput);
        Assert.Equal(28, c.CycleInputCostBasis);
        Assert.Equal(known, c.CycleInputCostBasisKnown);
        // The persisted cycle, not the input inventory still present later, owns this cost.
        var serialized = JsonSerializer.Serialize(c);
        var reloaded = JsonSerializer.Deserialize<Desertod.Domain.Entities.Citizen>(serialized)!;
        var report = Finish(reloaded, context);
        Assert.NotEmpty(report.Produced);
        var planks = reloaded.PersonalGoods.Single(g => g.Kind == ResourceKind.Planks);
        Assert.Equal(28, planks.CostBasis); Assert.Equal(known, planks.CostBasisKnown);
        Assert.Equal(101, reloaded.PersonalGoods.Sum(g => g.CostBasis));
        Assert.Null(CycleEngine.Settle(reloaded, context, reloaded.CycleEndsAt!.Value.AddDays(1)));
        Assert.Equal(101, reloaded.PersonalGoods.Sum(g => g.CostBasis));
    }

    [Fact]
    public void Unknown_legacy_history_is_not_relabelled_as_known_zero_by_warehouse_or_mixing()
    {
        var v = NewVillage(); var c = NewCitizen(v);
        var legacy = JsonSerializer.Deserialize<PersonalGood>("{\"Kind\":0,\"Amount\":4,\"CostBasis\":9}")!;
        legacy.Kind = ResourceKind.Wood; c.PersonalGoods.Add(legacy);
        Assert.False(legacy.CostBasisKnown);
        PersonalInventory.Add(c, ResourceKind.Wood, 1, 2);
        Assert.False(legacy.CostBasisKnown);
        var store = StorageBuilding.Create(StorageKind.WoodPile, "-4,10", Now); store.OwnerAccountId = c.AccountId;
        PrivateWarehouse.Deposit(c, store, ResourceKind.Wood, 5);
        Assert.False(store.PersonalCosts.Single().CostBasisKnown);
        PrivateWarehouse.Withdraw(c, store, ResourceKind.Wood, 5);
        Assert.False(c.PersonalGoods.Single().CostBasisKnown);
        Assert.Equal(11, c.PersonalGoods.Single().CostBasis);
    }
}
