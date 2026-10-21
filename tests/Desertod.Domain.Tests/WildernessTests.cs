using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class WildernessTests
{
    /// <summary>A small hand-made wild land: a level stretch of ravine, one thicket tile, one rockfall tile, the cave and the lookout.</summary>
    private static Village Wild()
    {
        var village = NewVillage();
        // The hand-authored lookout and walking points need stable underlying
        // terrain; a random village seed could put a pond at the passing point.
        village.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");
        village.Wilds = new Wilds
        {
            Ravine = Enumerable.Range(-12, 6).Select(x => new RavineSpan { X = x, Top = -20, Height = 2 }).ToList(),
            Obstacles =
            [
                new Obstacle { Kind = ObstacleKind.Thicket, X = -6, Y = 6 },
                new Obstacle { Kind = ObstacleKind.Rockfall, X = -8, Y = 14 },
            ],
            CaveX = -20,
            CaveY = 0,
            LookoutX = -30,
            LookoutY = 20,
        };
        return village;
    }

    private static void See(Village village, double x, double y) => Exploration.Reveal(village.Explored, new TilePoint(x, y), 3);

    private static bool Seen(Village village, double x, double y) => Exploration.IsExplored(village.Explored, (int)Math.Floor(x), (int)Math.Floor(y));

    [Fact]
    public void NewVillage_GetsItsWildLandOnce_TheSameForTheSameVillage()
    {
        var village = Village.Create("w", "W", Guid.NewGuid(), Now);
        var wilds = village.Wilds!;

        Assert.False(village.EnsureCamp(Now));
        Assert.Same(wilds, village.Wilds);

        var again = Wilderness.Generate(village.Id);
        Assert.Equal(wilds.Ravine.Select(s => (s.X, s.Top)), again.Ravine.Select(s => (s.X, s.Top)));
        Assert.Equal(wilds.Obstacles.Select(o => (o.X, o.Y)), again.Obstacles.Select(o => (o.X, o.Y)));
        Assert.Equal((wilds.CaveX, wilds.CaveY, wilds.LookoutX, wilds.LookoutY), (again.CaveX, again.CaveY, again.LookoutX, again.LookoutY));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GeneratedRavine_RunsUnbrokenFromTheWestEdgeToTheRiver_AndLeavesTheCampAlone(int seed)
    {
        var wilds = Wilderness.Generate(new Guid(seed, 0, 0, new byte[8]));

        Assert.Equal(WorldMap.Land.X, wilds.Ravine[0].X);
        for (var i = 1; i < wilds.Ravine.Count; i++)
        {
            Assert.Equal(wilds.Ravine[i - 1].X + 1, wilds.Ravine[i].X);
            Assert.InRange(Math.Abs(wilds.Ravine[i].Top - wilds.Ravine[i - 1].Top), 0, 1);
        }

        Assert.All(wilds.Ravine, span => Assert.True(span.Top + span.Height <= 0));
        Assert.Equal(3, wilds.Passes.Count);
        Assert.Equal(12, wilds.Obstacles.Count(o => o.Kind == ObstacleKind.Rockfall));
        Assert.Contains(wilds.Obstacles, o => o.Kind == ObstacleKind.Thicket);
        Assert.All(wilds.Obstacles, o =>
        {
            Assert.True(WorldMap.Land.Contains(o.Tile));
            Assert.False(WorldMap.BuildArea.Overlaps(o.Tile));
        });
        Assert.True(WorldMap.Land.Contains(new TileArea(wilds.CaveX, wilds.CaveY, 1, 1)));
        Assert.True(WorldMap.Land.Contains(new TileArea(wilds.LookoutX, wilds.LookoutY, 1, 1)));
    }

    [Fact]
    public void GeneratedObstacles_KeepOffTheCampLand_AndTheRoadEast()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var wilds = Wilderness.Generate(new Guid(seed, 7, 11, new byte[8]));

            Assert.All(wilds.Obstacles, o =>
            {
                Assert.False(o.X is >= 0 and < 44 && o.Y is >= 0 and < 26);
                Assert.False(o.X > 33 && o.Y is 12 or 13);
            });
        }
    }

    [Fact]
    public void UnseenThicket_CannotBeCleared()
    {
        var village = Wild();
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village };

        Assert.Equal("wilds.noThicket", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("clear-thicket"), context, Now)).Code);
    }

    [Fact]
    public void ClearingTheThicket_GivesWood_AndOpensTheWay()
    {
        var village = Wild();
        See(village, -5.5, 6.5);
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };

        CycleEngine.Start(citizen, Task("clear-thicket"), context, Now);
        var report = Finish(citizen, context);

        Assert.Equal(new TilePoint(-5.5, 6.5), citizen.Spot);
        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Wood && p.Amount > 0);
        Assert.NotNull(village.Wilds!.Obstacles[0].ClearedAt);
        Assert.Equal("wilds.noThicket", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("clear-thicket"), context, citizen.CycleEndsAt!.Value)).Code);
    }

    [Fact]
    public void PickedSpot_MustBeAtAStandingObstacle()
    {
        var village = Wild();
        See(village, -7.5, 14.5);
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village };

        Assert.Equal("spot.nothingToClear", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("break-rockfall"), context, Now, new TilePoint(-2.5, 14.5))).Code);
    }

    [Fact]
    public void Rockfall_TakesSeveralCycles_AndGivesStone()
    {
        var village = Wild();
        See(village, -7.5, 14.5);
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };
        var rockfall = village.Wilds!.Obstacles[1];
        var stone = 0.0;
        var cycles = 0;
        var now = Now;

        while (rockfall.ClearedAt is null && cycles < 20)
        {
            CycleEngine.Start(citizen, Task("break-rockfall"), context, now, new TilePoint(-7.5, 14.5));
            now = citizen.CycleEndsAt!.Value;
            stone += Finish(citizen, context).Produced.Where(p => p.Resource == ResourceKind.Stone).Sum(p => p.Amount);
            citizen.StoredEnergy = citizen.MaximumEnergy;
            cycles++;
        }

        Assert.NotNull(rockfall.ClearedAt);
        Assert.True(cycles > 1);
        Assert.True(stone > 0);
    }

    [Fact]
    public void Cave_IsDugOnceFound_MostlyForStone()
    {
        var village = Wild();
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };

        Assert.Equal("wilds.caveUnknown", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("dig-cave"), context, Now)).Code);

        See(village, -19.5, 0.5);
        CycleEngine.Start(citizen, Task("dig-cave"), context, Now);
        var report = Finish(citizen, context);

        Assert.Equal(village.Wilds!.CaveMouth, citizen.Spot);
        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Stone && p.Amount > 0);
        Assert.All(report.Produced, p => Assert.Contains(p.Resource, new[] { ResourceKind.Stone, ResourceKind.Coal, ResourceKind.IronOre }));
    }

    [Fact]
    public void RopeBridge_MustSpanTheRavine()
    {
        var village = Wild();
        Exploration.Reveal(village.Explored, new TilePoint(-9, -19), 5);

        Assert.Equal("building.bridgeNeedsRavine", Assert.Throws<GameRuleException>(() =>
            ConstructionRules.Place(village, BuildingKind.RopeBridge, 2, 2, Now)).Code);
        Assert.Equal("building.bridgeNeedsRavine", Assert.Throws<GameRuleException>(() =>
            ConstructionRules.Place(village, BuildingKind.RopeBridge, -10, -20, Now)).Code);

        var site = ConstructionRules.Place(village, BuildingKind.RopeBridge, -10, -21, Now);

        Assert.Equal(BuildingKind.RopeBridge, site.Kind);
        Assert.Equal(["posts", "ropes", "deck"], site.Definition.Stages.Select(s => s.Id));
    }

    [Fact]
    public void ClimbingTheLookout_RevealsTheLandFarAround()
    {
        var village = Wild();
        var lookout = village.Wilds!.Lookout;

        village.TsarGoTo(lookout, Now);
        var arrived = village.TsarTrip!.ArrivesAt;
        village.Explore([], arrived);

        Assert.True(Seen(village, lookout.X + 20, lookout.Y));
        Assert.True(Seen(village, lookout.X, lookout.Y - 20));
    }

    [Fact]
    public void WalkingPastTheLookout_DoesNotRevealTheFarLand()
    {
        var village = Wild();
        var lookout = village.Wilds!.Lookout;

        village.TsarGoTo(new TilePoint(lookout.X + 4, lookout.Y), Now);
        village.Explore([], village.TsarTrip!.ArrivesAt);

        Assert.False(Seen(village, lookout.X, lookout.Y - 20));
    }
}
