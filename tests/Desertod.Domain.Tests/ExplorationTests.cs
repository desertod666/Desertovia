using Desertod.Domain.Buildings;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class ExplorationTests
{
    private static bool Seen(byte[] map, TilePoint point) => Exploration.IsExplored(map, (int)Math.Floor(point.X), (int)Math.Floor(point.Y));

    [Fact]
    public void NewTribe_KnowsTheCampLand_ButNotTheWildBeyond()
    {
        var village = NewVillage();

        Assert.True(Seen(village.Explored, WorldMap.Places[WorldMap.Camp]));
        Assert.True(Seen(village.Explored, WorldMap.Places["stones"]));
        Assert.True(Seen(village.Explored, WorldMap.Places[WorldMap.Ford]));
        Assert.False(Seen(village.Explored, WorldMap.Places[WorldMap.GoldVein]));
        Assert.False(Seen(village.Explored, WorldMap.Places["deer-woods"]));
    }

    [Fact]
    public void WalkedLand_IsSeenOnlyOnceTheWalkerGetsThere()
    {
        var village = NewVillage();
        // A fixed reachable point below the ravine. The former northern destination
        // could sit behind an uncleared pass, which a real walk must not cross.
        village.Id = Guid.Parse("11111111-1111-4111-8111-111111111111");
        village.Wilds = Wilderness.Generate(village.Id);
        var far = new TilePoint(-30, 20);

        village.TsarGoTo(far, Now);
        var trip = village.TsarTrip!;

        Assert.False(Seen(Exploration.Seen(village.Explored, [trip], Now), far));
        var halfway = Now + (trip.ArrivesAt - Now) / 2;
        Assert.False(Seen(Exploration.Seen(village.Explored, [trip], halfway), far));
        Assert.True(Seen(Exploration.Seen(village.Explored, [trip], halfway), trip.PositionAt(halfway)));
        Assert.True(Seen(Exploration.Seen(village.Explored, [trip], trip.ArrivesAt), far));
    }

    [Fact]
    public void TheNextWalk_KeepsWhatTheLastOneSaw()
    {
        var village = NewVillage();
        // A fixed reachable point below the ravine. The former northern destination
        // could sit behind an uncleared pass, which a real walk must not cross.
        village.Id = Guid.Parse("11111111-1111-4111-8111-111111111111");
        village.Wilds = Wilderness.Generate(village.Id);
        var far = new TilePoint(-30, 20);
        village.TsarGoTo(far, Now);
        var arrived = village.TsarTrip!.ArrivesAt;

        village.TsarGoTo(WorldMap.Places[WorldMap.Camp], arrived);

        Assert.True(Seen(village.Explored, far));
        Assert.Equal(far, village.TsarTrip!.From);
    }

    [Fact]
    public void Blueprint_NeedsSeenLand()
    {
        var village = NewVillage();

        Assert.Equal("building.unexplored",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.WoodPile, 0, 0, Now)).Code);

        village.TsarGoTo(new TilePoint(1.5, 2), Now);
        village.Explore([], village.TsarTrip!.ArrivesAt);

        Assert.Equal(BuildingKind.WoodPile, ConstructionRules.Place(village, BuildingKind.WoodPile, 0, 0, Now).Kind);
    }

    [Fact]
    public void CitizenGoingToWork_SeesTheWorkplaceOnArrival()
    {
        var village = NewVillage();
        // Use the same reachable seeded workplace as the other exploration walks.
        // A random northern forest may be separated by an uncleared ravine.
        village.Id = Guid.Parse("11111111-1111-4111-8111-111111111111");
        village.Wilds = Wilderness.Generate(village.Id);
        var citizen = NewCitizen(village);
        var workplace = new TilePoint(-30, 20);

        citizen.GoTo(workplace, village, Now);

        Assert.False(Seen(Exploration.Seen(village.Explored, [citizen.Trip!], Now), workplace));
        Assert.True(Seen(Exploration.Seen(village.Explored, [citizen.Trip!], citizen.Trip!.ArrivesAt), workplace));
    }
}
