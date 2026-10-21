using System.Text.Json;
using System.Security.Cryptography;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Rules;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class RegionalWorldTests
{
    public static IEnumerable<object[]> TerrainVectors()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"regional-terrain.json")))
        .RootElement.EnumerateArray().Select(row=>new object[]{row.GetProperty("seed").GetUInt32(),row.GetProperty("extraHash").GetString()!}).ToArray();

    [Theory]
    [MemberData(nameof(TerrainVectors))]
    public void Every_new_tile_matches_the_independent_geometry_vectors(uint seed,string expectedHash)
    {
        var extra=new byte[7200];
        for(var y=58;y<118;y++)for(var x=-44;x<76;x++)extra[(y-58)*120+x+44]=RegionalTerrain.Kind(seed,x,y);
        Assert.Equal(expectedHash,Convert.ToHexStringLower(SHA256.HashData(extra)));
    }

    [Fact]
    public void Upgrade_preserves_every_old_fog_bit_and_adds_only_unknown_land()
    {
        var old=new byte[Exploration.LegacySize];new Random(42).NextBytes(old);
        var upgraded=Exploration.Upgrade(old);Assert.Equal(2250,upgraded.Length);
        Assert.Equal(old,upgraded[..old.Length]);Assert.All(upgraded[old.Length..],b=>Assert.Equal(0,b));
        Assert.Same(upgraded,Exploration.Upgrade(upgraded));
        for(var y=WorldMap.LegacyLand.Y;y<58;y++)for(var x=WorldMap.LegacyLand.X;x<76;x++)
        {var index=(y+32)*120+x+44;Assert.Equal((old[index>>3]&(1<<(index&7)))!=0,Exploration.IsExplored(upgraded,x,y));}
        var v=NewVillage();v.Explored=old;var wilds=JsonSerializer.Serialize(v.Wilds);v.EnsureCamp(Now);
        // EnsureCamp may reveal existing buildings, but never discards a discovered old tile.
        for(var i=0;i<old.Length;i++)Assert.Equal(old[i],(byte)(v.Explored[i]&old[i]));
        Assert.Equal(wilds,JsonSerializer.Serialize(v.Wilds));
    }

    [Theory]
    [InlineData("11111111-1111-4111-8111-111111111111")]
    [InlineData("22222222-2222-4222-8222-222222222222")]
    public void Expansion_keeps_the_complete_original_collision_and_adds_distinct_reachable_resources(string id)
    {
        var village=Guid.Parse(id);var wilds=Wilderness.Generate(village);var original=AuthoredTerrain.For(village,wilds);
        var expanded=RegionalTerrain.For(village,wilds);Assert.Equal(18000,expanded.Length);Assert.Equal(original,expanded[..original.Length]);
        Assert.Equal(expanded,RegionalTerrain.For(village,wilds));
        Assert.Equal(5,expanded[RegionalTerrain.Index(8,94)]);Assert.Equal(3,expanded[RegionalTerrain.Index(54,103)]);
        Assert.Equal(AuthoredTerrain.Marsh,expanded[RegionalTerrain.Index(-13,82)]);
    }

    [Fact]
    public void Routes_avoid_water_and_walls_and_require_the_actual_bridge()
    {
        var v=NewVillage();
        Assert.Equal("bridge.closed",Assert.Throws<Desertod.Domain.GameRuleException>(()=>RegionalTravel.Find(v,WorldMap.Places[WorldMap.Camp],RegionalTerrain.QuarryTrail)).Code);
        v.BridgeOpened=true;
        var path=RegionalTravel.Find(v,WorldMap.Places[WorldMap.Camp],RegionalTerrain.QuarryTrail);
        var nav=new RaidNavigation(v,true);
        var previous=WorldMap.Places[WorldMap.Camp];var crossed=false;
        foreach(var point in path)
        {
            var count=Math.Max(1,(int)Math.Ceiling(previous.DistanceTo(point.Point)*4));
            for(var i=0;i<=count;i++)
            {
                var x=previous.X+(point.X-previous.X)*i/count;var y=previous.Y+(point.Y-previous.Y)*i/count;
                Assert.True(nav.Pass((int)Math.Floor(x),(int)Math.Floor(y)));
                crossed|=x>=30&&x<34&&y>=12&&y<14;
            }
            previous=point.Point;
        }
        Assert.True(crossed);
        v.BridgeRuinedAt=Now;
        Assert.Equal("bridge.closed",Assert.Throws<Desertod.Domain.GameRuleException>(()=>RegionalTravel.Find(v,WorldMap.Places[WorldMap.Camp],RegionalTerrain.QuarryTrail)).Code);
    }

    [Fact]
    public void A_saved_journey_reveals_only_walked_segments_and_returns_without_teleport()
    {
        var v=NewVillage();v.TsarGoTo(RegionalTerrain.MarshTrail,Now);var trip=v.TsarTrip!;
        Assert.True(trip.Waypoints.Count>2);Assert.True(trip.ArrivesAt>Now.AddSeconds(40));
        var half=Now+(trip.ArrivesAt-Now)/2;var saved=JsonSerializer.Deserialize<Trip>(JsonSerializer.Serialize(trip))!;
        Assert.Equal(trip.ArrivesAt,saved.ArrivesAt);Assert.Equal(trip.PositionAt(half),saved.PositionAt(half));
        Assert.NotEqual(trip.To,saved.PositionAt(half));
        var seen=Exploration.Seen(v.Explored,[saved],half);
        Assert.False(Exploration.IsExplored(seen,14,92));
        v.TsarTrip=saved;v.TsarGoTo(WorldMap.Places[WorldMap.Camp],half);
        Assert.Equal(saved.PositionAt(half),v.TsarTrip!.From);
        var arrived=v.TsarTrip.ArrivesAt;v.Explore([],arrived);
        Assert.Equal(WorldMap.Places[WorldMap.Camp],v.TsarTrip.PositionAt(arrived));
        Assert.False(v.IsExplored(new(14,92,1,1)));
    }

    [Fact]
    public void Rounded_arrival_reaches_the_exact_endpoint_but_an_earlier_obstruction_still_freezes_the_route()
    {
        for(var n=1;n<=128;n++)
        {
            var trip=Trip.Start(new(1d/n,1d/(n+1)),new(15.5,13.5),Now);
            trip.Waypoints=[new(){X=7.5,Y=1d/(n+2),Cost=2.5},new(){X=15.5,Y=13.5}];
            Assert.Equal(trip.To,trip.PositionAt(trip.ArrivesAt));
            trip.BlockedAt=trip.SetOutAt+(trip.ArrivesAt-trip.SetOutAt)/2;
            Assert.NotEqual(trip.To,trip.PositionAt(trip.ArrivesAt));
        }
    }

    [Fact]
    public void Blocked_destination_and_outside_coordinates_leave_the_old_trip_and_fog_unchanged()
    {
        var v=NewVillage();var fog=(byte[])v.Explored.Clone();
        v.Fortifications.Add(new(){Id="fixture-wall",X=14,Y=92});
        Assert.Equal("spot.noPath",Assert.Throws<Desertod.Domain.GameRuleException>(()=>v.TsarGoTo(RegionalTerrain.MarshTrail,Now)).Code);
        Assert.Null(v.TsarTrip);Assert.Equal(fog,v.Explored);
        Assert.Equal("spot.outsideLand",Assert.Throws<Desertod.Domain.GameRuleException>(()=>v.TsarGoTo(new(5,200),Now)).Code);
    }

    [Fact]
    public void Regional_gathering_uses_real_tile_resources_and_waits_for_the_saved_walk()
    {
        var v=NewVillage();var worker=NewCitizen(v);var task=GameCatalog.FindTask("cut-reeds")!;
        // Catalog ID is asserted: a renamed task cannot silently remove coverage.
        Assert.NotNull(task);
        var reeds=new TilePoint(8.5,94.5);
        WorkSpots.Check(v,task,reeds,Now);
        Assert.Equal("spot.notForTask",Assert.Throws<Desertod.Domain.GameRuleException>(()=>WorkSpots.Check(v,task,RegionalTerrain.MarshTrail,Now)).Code);
        CycleEngine.Start(worker,task,new(){Village=v},Now,reeds);
        Assert.True(worker.Trip!.Waypoints.Count>0);Assert.True(worker.CycleEndsAt>=worker.Trip.ArrivesAt);
        Assert.Null(CycleEngine.Settle(worker,new(){Village=v},Now.AddSeconds(1)));
    }

    [Fact]
    public void Old_json_trip_without_waypoints_keeps_its_accepted_timing()
    {
        var old=JsonSerializer.Deserialize<Trip>("""{"FromX":1,"FromY":1,"ToX":11,"ToY":1,"SpeedMultiplier":1,"SetOutAt":"2026-01-01T12:00:00Z"}""")!;
        Assert.Empty(old.Waypoints);Assert.Equal(Now.AddSeconds(6),old.ArrivesAt);Assert.Equal(new TilePoint(6,1),old.PositionAt(Now.AddSeconds(3)));
    }
}
