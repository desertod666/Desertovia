// © 2026 Desertovia
// Tools in hand speed the matching gather and break after their uses run out.

using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class ToolDurabilityTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(149, 1)]
    public void Pickaxe_ExtractsOnlyTheGoldActuallyRemovedFromTheVein(int mined, int expected)
    {
        var (citizen, village) = Gatherer();
        village.GoldVeinMined = mined;
        citizen.Carry(ResourceKind.SteelPickaxe, 1);
        var context = new WorkContext { Village = village, RecentCitizenCount = 1,
            CompletedProjects = new HashSet<string> { ProjectCatalog.TsarBridge } };
        CycleEngine.Start(citizen, Task(GoldVein.DigTask), context, Now);
        var report = Finish(citizen, context);
        Assert.Equal(expected, Assert.Single(report.Produced).Amount);
        Assert.Equal(mined + expected, village.GoldVeinMined);
        Assert.Equal(19, Tool(citizen, ResourceKind.SteelPickaxe).Durability);
    }

    private static (Citizen Citizen, Village Village) Gatherer()
    {
        var village = NewVillage();
        // Strength and endurance 5 make the stat factor 1, so the tool multiplier is the yield.
        var citizen = NewCitizen(village, (Stat.Strength, 5), (Stat.Endurance, 5));
        return (citizen, village);
    }

    private static CarriedGood Tool(Citizen citizen, ResourceKind kind) =>
        citizen.Load.Single(good => good.Kind == kind);

    [Fact]
    public void WoodenAxe_WearsOncePerChop_AndDoesNotSpeedIt()
    {
        var (citizen, village) = Gatherer();
        citizen.Carry(ResourceKind.WoodenAxe, 1);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        var first = Finish(citizen, WorkContext.For(village));

        Assert.Equal(5, first.Produced.Single().Amount);
        Assert.Empty(first.ToolsBroken);
        Assert.Equal(4, Tool(citizen, ResourceKind.WoodenAxe).Durability);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), citizen.CycleEndsAt!.Value);
        Finish(citizen, WorkContext.For(village));
        Assert.Equal(3, Tool(citizen, ResourceKind.WoodenAxe).Durability);
    }

    [Fact]
    public void StoneAxe_SpeedsWood_AndPickaxeDoesNot()
    {
        var (citizen, village) = Gatherer();
        citizen.Carry(ResourceKind.StoneAxe, 1);
        citizen.Carry(ResourceKind.SteelPickaxe, 1);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.Equal(6, report.Produced.Single().Amount);
        Assert.Equal(7, Tool(citizen, ResourceKind.StoneAxe).Durability);
        Assert.Equal(20, Tool(citizen, ResourceKind.SteelPickaxe).Durability);
    }

    [Fact]
    public void IronAndSteelAxes_ApplyTheirSpeed()
    {
        var (ironCitizen, ironVillage) = Gatherer();
        ironCitizen.Carry(ResourceKind.IronAxe, 1);
        CycleEngine.Start(ironCitizen, Task("chop-wood"), WorkContext.For(ironVillage), Now);
        Assert.Equal(8, Finish(ironCitizen, WorkContext.For(ironVillage)).Produced.Single().Amount);
        Assert.Equal(11, Tool(ironCitizen, ResourceKind.IronAxe).Durability);

        var (steelCitizen, steelVillage) = Gatherer();
        steelCitizen.Carry(ResourceKind.SteelAxe, 1);
        CycleEngine.Start(steelCitizen, Task("chop-wood"), WorkContext.For(steelVillage), Now);
        Assert.Equal(9, Finish(steelCitizen, WorkContext.For(steelVillage)).Produced.Single().Amount);
        Assert.Equal(19, Tool(steelCitizen, ResourceKind.SteelAxe).Durability);
    }

    [Fact]
    public void StonePickaxe_SpeedsStone_AndAxeDoesNot()
    {
        var (citizen, village) = Gatherer();
        citizen.Carry(ResourceKind.StonePickaxe, 1);
        citizen.Carry(ResourceKind.SteelAxe, 1);

        CycleEngine.Start(citizen, Task("quarry-stone"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.Equal(5, report.Produced.Single().Amount);
        Assert.Equal(7, Tool(citizen, ResourceKind.StonePickaxe).Durability);
        Assert.Equal(20, Tool(citizen, ResourceKind.SteelAxe).Durability);
    }

    [Fact]
    public void BestAxe_IsUsed_AndAWornSpareStaysSeparate()
    {
        var (citizen, village) = Gatherer();
        citizen.Carry(ResourceKind.WoodenAxe, 1);
        citizen.Carry(ResourceKind.SteelAxe, 2);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        Finish(citizen, WorkContext.For(village));

        Assert.Equal(5, Tool(citizen, ResourceKind.WoodenAxe).Durability);
        var steel = citizen.Load.Where(good => good.Kind == ResourceKind.SteelAxe).OrderBy(good => good.Durability).ToList();
        Assert.Equal(2, steel.Count);
        Assert.Equal((19, 1), (steel[0].Durability, steel[0].Amount));
        Assert.Equal((20, 1), (steel[1].Durability, steel[1].Amount));
    }

    [Fact]
    public void Axe_BreaksAfterItsUses_AndTheNextChopIsBareHanded()
    {
        var (citizen, village) = Gatherer();
        var (bareHands, bareVillage) = Gatherer();
        citizen.Carry(ResourceKind.WoodenAxe, 1);
        var when = Now;
        var bareWhen = Now;
        CycleReport? last = null;
        for (var use = 0; use < 5; use++)
        {
            CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), when);
            last = Finish(citizen, WorkContext.For(village));
            when = citizen.CycleEndsAt!.Value;
            CycleEngine.Start(bareHands, Task("chop-wood"), WorkContext.For(bareVillage), bareWhen);
            Finish(bareHands, WorkContext.For(bareVillage));
            bareWhen = bareHands.CycleEndsAt!.Value;
        }

        Assert.Equal([ResourceKind.WoodenAxe], last!.ToolsBroken);
        Assert.DoesNotContain(citizen.Load, good => good.Kind == ResourceKind.WoodenAxe);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), when);
        var afterBreak = Finish(citizen, WorkContext.For(village));
        CycleEngine.Start(bareHands, Task("chop-wood"), WorkContext.For(bareVillage), bareWhen);
        var neverHadAxe = Finish(bareHands, WorkContext.For(bareVillage));
        Assert.Empty(afterBreak.ToolsBroken);
        Assert.Equal(neverHadAxe.Produced.Single().Amount, afterBreak.Produced.Single().Amount);
    }

    [Fact]
    public void WornTool_KeepsItsWearThroughStorage()
    {
        var (citizen, village) = Gatherer();
        citizen.Carry(ResourceKind.StoneAxe, 1);
        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        Finish(citizen, WorkContext.For(village));

        StorageRules.Unload(citizen, village);
        Assert.Empty(citizen.Load);
        var stored = village.Storages.SelectMany(storage => storage.Goods).Single(good => good.Kind == ResourceKind.StoneAxe);
        Assert.Equal(7, stored.Durability);

        var pile = village.Storages.Single(storage => storage.Goods.Any(good => good.Kind == ResourceKind.StoneAxe));
        StorageRules.Take(citizen, village, pile.PlotId, ResourceKind.StoneAxe, 1, citizen.CycleEndsAt!.Value);
        Assert.Equal(7, Tool(citizen, ResourceKind.StoneAxe).Durability);
    }

    [Fact]
    public void WoodenAxe_IsCraftedAtFullDurability()
    {
        var (citizen, village) = Gatherer();
        village.Store(ResourceKind.Wood, 2);

        CycleEngine.Start(citizen, Task("make-wooden-axe"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.Equal(ResourceKind.WoodenAxe, report.Produced.Single().Resource);
        Assert.Equal(5, report.Produced.Single().Durability);
        var stored = village.Storages.SelectMany(storage => storage.Goods).Single(good => good.Kind == ResourceKind.WoodenAxe);
        Assert.Equal(1, stored.Amount);
        Assert.Equal(5, stored.Durability);
        Assert.Equal(0, village.Stock(ResourceKind.Wood));
    }

    [Fact]
    public void StoneAxe_NeedsTheStoneToolsDiscovery()
    {
        var (citizen, village) = Gatherer();
        village.Store(ResourceKind.Wood, 1);
        village.Store(ResourceKind.Stone, 2);

        var missing = Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("make-stone-axe"), WorkContext.For(village), Now));
        Assert.Equal("task.needsTechnology", missing.Code);

        village.Research.Add(new TechnologyProgress { TechnologyId = TechnologyCatalog.StoneTools, ResearchedAt = Now });
        CycleEngine.Start(citizen, Task("make-stone-axe"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));
        Assert.Equal(8, report.Produced.Single().MaxDurability);
    }
}
