using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class ForestTests
{
    private static Village Grove(params (int X, int Y)[] tiles)
    {
        var village = NewVillage();
        village.Trees.AddRange(tiles.Select(tile => new Tree { X = tile.X, Y = tile.Y }));
        return village;
    }

    [Fact]
    public void NewLand_HasTreesInTheForest_ButNotInTheClearing()
    {
        var trees = Forest.Plant(new Random(7));

        Assert.True(trees.Count(t => t.X < 8 && t.Y < 12) > 40);
        Assert.DoesNotContain(trees, t => t.X is >= 12 and < 19 && t.Y is >= 10 and < 17);
        Assert.All(trees, t => Assert.True(Forest.Area.Contains(t.Tile)));
    }

    [Fact]
    public void OldVillage_GetsItsTreesOnce()
    {
        var village = NewVillage();

        Assert.True(village.EnsureCamp(Now));
        Assert.NotEmpty(village.Trees);
        Assert.False(village.EnsureCamp(Now));
    }

    [Fact]
    public void TreesOnABlueprint_AddAFellingJob_ThatBringsTheirWood()
    {
        var village = Grove((10, 12), (11, 13));
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village, RecentCitizenCount = 1 };

        var site = ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);

        Assert.Equal(2, site.TreesToFell);
        Assert.Equal(BuildingCatalog.FellTrees, site.Jobs(0)[0].JobId);
        Assert.Equal(2 * BuildingCatalog.FellWorkPerTree, site.Required(0, BuildingCatalog.FellTrees));
        Assert.Empty(Forest.Visible(village, Now));
        Assert.Equal(2, Forest.Drawn(village, Now).Count());

        var now = Now;
        var wood = 0.0;
        for (var cycle = 0; cycle < 50 && site.Remaining(0, BuildingCatalog.FellTrees) > 1e-6; cycle++)
        {
            CycleEngine.StartJob(citizen, site.PlotId, 0, BuildingCatalog.FellTrees, context, now);
            now = citizen.CycleEndsAt!.Value;
            wood += Finish(citizen, context).Produced.Where(p => p.Resource == ResourceKind.Wood).Sum(p => p.Amount);
        }

        Assert.Equal(2 * Forest.WoodPerTree, wood, 6);
        Assert.Empty(Forest.Drawn(village, now));
        Assert.Empty(Forest.Drawn(village, now + Forest.RegrowTime));
        Assert.False(site.JobsDone(0));
    }

    [Fact]
    public void RegrownTree_OnABlueprint_StaysStanding_UntilTheBlueprintIsWithdrawn()
    {
        var village = Grove((10, 12));
        village.Trees[0].FelledAt = Now - Forest.RegrowTime * 2;

        var site = ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);

        Assert.Equal(1, site.TreesToFell);
        Assert.Single(Forest.Drawn(village, Now));

        ConstructionRules.Cancel(village, site.PlotId);

        Assert.Single(Forest.Visible(village, Now));
    }

    [Fact]
    public void BareBlueprint_HasNoFellingJob()
    {
        var village = Grove((2, 22));

        var site = ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);

        Assert.Equal(0, site.TreesToFell);
        Assert.DoesNotContain(site.Jobs(0), job => job.JobId == BuildingCatalog.FellTrees);
    }

    [Fact]
    public void GatheringWood_ChopsTheTreeNearestTheForest_AndFellsItWhenItsWoodIsGone()
    {
        var forest = WorldMap.Places[Forest.Workplace];
        var village = Grove(((int)forest.X, (int)forest.Y), (20, 20));
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };
        var near = village.Trees[0];
        var gathered = 0.0;
        var now = Now;

        while (near.FelledAt is null)
        {
            CycleEngine.Start(citizen, Task("chop-wood"), context, now);
            now = citizen.CycleEndsAt!.Value;
            gathered += Finish(citizen, context).Produced.Sum(p => p.Amount);
        }

        Assert.True(gathered >= Forest.WoodPerTree - 1e-6);
        Assert.Equal(new[] { (20, 20) }, Forest.Visible(village, now).Select(t => (t.X, t.Y)));
    }

    [Fact]
    public void FelledTree_GrowsBack_UnlessSomethingIsBuiltOverIt()
    {
        var village = Grove((10, 12), (2, 22));
        Forest.Chop(village, Forest.WoodPerTree * 2, Now);
        ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);

        var later = Now + Forest.RegrowTime;

        Assert.Equal(new[] { (2, 22) }, Forest.Visible(village, later).Select(t => (t.X, t.Y)));
        Assert.Single(Forest.Standing(village, new TileArea(2, 22, 1, 1), later));
        Assert.Empty(Forest.Standing(village, new TileArea(10, 12, 1, 1), later));
    }

    [Fact]
    public void PickedTree_IsChopped_AndTheWalkStartsThere()
    {
        var village = Grove((5, 8), (12, 8));
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };
        var spot = new TilePoint(12.5, 8.5);

        CycleEngine.Start(citizen, Task("chop-wood"), context, Now, spot);
        var walk = citizen.CycleEndsAt!.Value - Now - TimeSpan.FromSeconds(Task("chop-wood").WorkSeconds);
        Finish(citizen, context);

        Assert.Equal(spot, citizen.Spot);
        Assert.True(walk < CycleEngine.Walk(citizen, village, Task("chop-wood")));
        Assert.True(village.Trees.Single(t => t.X == 12).Chopped > 0);
        Assert.Equal(0, village.Trees.Single(t => t.X == 5).Chopped);
    }

    [Fact]
    public void Spot_MustBeAStandingTree_OnTheCampLand()
    {
        var village = Grove((5, 8));
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village };

        Assert.Equal("spot.noTree", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("chop-wood"), context, Now, new TilePoint(20.5, 15.5))).Code);
        Assert.Equal("spot.notForTask", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("tend-fire"), context, Now, new TilePoint(5.5, 8.5))).Code);
        Assert.Equal("spot.outsideLand", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("forage"), context, Now, new TilePoint(500, 500))).Code);
    }

    [Fact]
    public void UncheckedSpot_NeverShortensTheWalk()
    {
        var village = NewVillage();
        var context = new WorkContext { Village = village };
        var picked = NewCitizen(village);
        var plain = NewCitizen(village);

        CycleEngine.Start(picked, Task("forage"), context, Now, new TilePoint(14.5, 9.5));
        CycleEngine.Start(plain, Task("forage"), context, Now);

        Assert.Equal(plain.CycleEndsAt, picked.CycleEndsAt);
    }

    [Fact]
    public void WoodFromTheWild_FellsNoCampTree()
    {
        var village = Grove((5, 8));
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };

        // Outside the managed grove, on the guaranteed dry southern camp approach.
        // An arbitrary western point can be a pond for this world's seed.
        var wild = new TilePoint(15.5, 26.5);
        Assert.False(Forest.Area.Contains(wild.ToTile()));
        CycleEngine.Start(citizen, Task("chop-wood"), context, Now, wild);
        var report = Finish(citizen, context);

        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Wood && p.Amount > 0);
        Assert.Equal(0, village.Trees[0].Chopped);
    }

    [Fact]
    public void NextTask_ForgetsTheSpot()
    {
        var village = Grove((5, 8));
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village };

        CycleEngine.Start(citizen, Task("chop-wood"), context, Now, new TilePoint(5.5, 8.5));
        Finish(citizen, context);
        CycleEngine.Start(citizen, Task("tend-fire"), context, citizen.CycleEndsAt!.Value);

        Assert.Null(citizen.Spot);
    }

    [Fact]
    public void BareLand_StillGivesWood()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };

        CycleEngine.Start(citizen, Task("chop-wood"), context, Now);
        var report = Finish(citizen, context);

        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Wood && p.Amount > 0);
    }
}
