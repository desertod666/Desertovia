using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class GroundRouteTests
{
    private static void Walkable(Village village,Trip trip)
    {
        var nav=new RaidNavigation(village,true);
        foreach(var (from,to) in trip.WalkedSegments(trip.ArrivesAt))
        {
            var samples=Math.Max(1,(int)Math.Ceiling(from.DistanceTo(to)*8));
            for(var i=0;i<=samples;i++)
                Assert.True(nav.Pass((int)Math.Floor(from.X+(to.X-from.X)*i/samples),(int)Math.Floor(from.Y+(to.Y-from.Y)*i/samples)),$"Blocked segment {from}→{to}");
        }
    }

    [Fact]
    public void New_core_walks_of_the_resident_and_tsar_follow_saved_ground_around_a_closed_wall()
    {
        var v=NewVillage();var c=NewCitizen(v);var target=new TilePoint(8.5,13.5);
        for(var y=9;y<=17;y++)v.Fortifications.Add(new(){Id=$"fixture-{y}",X=11,Y=y});
        v.TsarGoTo(target,Now);c.GoTo(target,v,Now);
        foreach(var trip in new[]{v.TsarTrip!,c.Trip!})
        {
            Assert.NotEmpty(trip.Waypoints);Assert.Equal(target,trip.PositionAt(trip.ArrivesAt));Walkable(v,trip);
            Assert.True(trip.Duration>trip.From.DistanceTo(target)*Exploration.SecondsPerTile);
            var loaded=JsonSerializer.Deserialize<Trip>(JsonSerializer.Serialize(trip))!;
            Assert.Equal(trip.Id,loaded.Id);Assert.Equal(trip.ArrivesAt,loaded.ArrivesAt);
            Assert.Equal(trip.PositionAt(Now.AddSeconds(1)),loaded.PositionAt(Now.AddSeconds(1)));
        }
    }

    [Fact]
    public void A_solid_core_destination_is_rejected_without_redirecting_or_revealing_the_previous_walk()
    {
        var v=NewVillage();var c=NewCitizen(v);var target=new TilePoint(8.5,13.5);
        v.Fortifications.Add(new(){Id="fixture-destination",X=8,Y=13});
        var old=Trip.Start(WorldMap.Places[WorldMap.Camp],new(20.5,13.5),Now.AddSeconds(-1));
        v.TsarTrip=old;c.Trip=JsonSerializer.Deserialize<Trip>(JsonSerializer.Serialize(old));
        var residentBefore=JsonSerializer.Serialize(c.Trip);var fog=(byte[])v.Explored.Clone();
        Assert.Equal("spot.noPath",Assert.Throws<GameRuleException>(()=>v.TsarGoTo(target,Now)).Code);
        Assert.Equal("spot.noPath",Assert.Throws<GameRuleException>(()=>c.GoTo(target,v,Now)).Code);
        Assert.Same(old,v.TsarTrip);Assert.Equal(residentBefore,JsonSerializer.Serialize(c.Trip));Assert.Equal(fog,v.Explored);
    }

    [Fact]
    public void Closing_a_core_gate_stops_the_walk_before_it_and_resume_keeps_the_actual_position()
    {
        var v=NewVillage();var target=new TilePoint(8.5,13.5);
        for(var y=9;y<=17;y++)v.Fortifications.Add(new(){Id=$"fixture-{y}",X=11,Y=y,Gate=y==13,Open=y==13});
        v.TsarGoTo(target,Now);var old=v.TsarTrip!;var gate=v.Fortifications.Single(f=>f.Gate);
        gate.Open=false;Assert.True(RegionalTravel.Reconcile(v,old,Now.AddSeconds(.1)));
        Assert.Equal("gate",old.BlockReason);var blocked=old.PositionAt(Now.AddMinutes(1));Assert.True(blocked.X>=12);
        var resumed=RegionalTravel.Resume(v,old,Now.AddMinutes(1));Assert.Equal(old.Id,resumed.Id);Assert.Equal(blocked,resumed.From);
        Assert.Equal(target,resumed.To);Assert.Null(resumed.BlockedAt);Walkable(v,resumed);
    }

    [Fact]
    public void Clearing_an_obstacle_walks_to_its_accessible_edge_while_retaining_the_chosen_work_tile()
    {
        var v=NewVillage();var c=NewCitizen(v);var target=new TilePoint(8.5,13.5);
        v.Wilds!.Obstacles.Add(new(){Kind=ObstacleKind.Thicket,X=8,Y=13});
        CycleEngine.Start(c,GameCatalog.FindTask("clear-thicket")!,new(){Village=v},Now,target);
        Assert.Equal(target,c.Spot);Assert.NotEmpty(c.Trip!.Waypoints);Assert.NotEqual(target,c.Trip.To);
        Assert.InRange(c.Trip.To.DistanceTo(target),.1,1.5);Assert.True(c.CycleEndsAt>=c.Trip.ArrivesAt);Walkable(v,c.Trip);
    }

    [Fact]
    public void Pier_work_uses_a_dry_approach_without_allowing_a_manual_walk_into_the_river()
    {
        var v=NewVillage();var c=NewCitizen(v);var pier=WorldMap.Places[WorldMap.Pier];
        Assert.Equal("spot.noPath",Assert.Throws<GameRuleException>(()=>c.GoTo(pier,v,Now)).Code);
        Assert.Null(c.Trip);
        CycleEngine.Start(c,GameCatalog.FindTask("fish")!,new(){Village=v,CompletedProjects=new HashSet<string>{ProjectCatalog.FishingPier}},Now);
        Assert.NotEmpty(c.Trip!.Waypoints);Assert.InRange(c.Trip.To.DistanceTo(pier),0,1.5);Walkable(v,c.Trip);
        Assert.Null(CycleEngine.Settle(c,new(){Village=v},Now.AddSeconds(1)));
    }

    [Fact]
    public void A_previously_accepted_straight_trip_is_not_rewritten_when_new_core_paths_are_enabled()
    {
        var v=NewVillage();var old=Trip.Start(new(8.5,13.5),new(20.5,13.5),Now);
        v.TsarTrip=old;v.Fortifications.Add(new(){Id="later-wall",X=11,Y=13});
        var json=JsonSerializer.Serialize(old);Assert.False(RegionalTravel.Reconcile(v,old,Now.AddSeconds(1)));
        Assert.Equal(json,JsonSerializer.Serialize(old));Assert.Empty(old.Waypoints);Assert.Equal(new TilePoint(13.5,13.5),old.PositionAt(Now.AddSeconds(3)));
        v.TsarGoTo(new(22.5,13.5),Now.AddSeconds(3));Assert.NotEmpty(v.TsarTrip!.Waypoints);
        Assert.Equal(old.PositionAt(Now.AddSeconds(3)),v.TsarTrip.From);
    }
    [Fact]
    public void Long_inbound_work_walk_applies_the_medical_speed_once_and_cannot_pay_before_arrival()
    {
        var v=NewVillage();var c=NewCitizen(v);var task=GameCatalog.FindTask("chop-wood")!;
        c.IdleSpot=new(14.5,117.5);
        c.Recovery=new ExternalRecovery{Available=true,EffectKind="cast",EffectUntil=Now.AddHours(2)};
        var work=SkillRules.WorkTime(c,task.Skill,task.SkillLevel,TimeSpan.FromSeconds(task.WorkSeconds));
        CycleEngine.Start(c,task,new(){Village=v},Now);
        Assert.False(c.CycleTired);Assert.Equal(.8,c.Trip!.SpeedMultiplier);
        Assert.True(c.Trip.ArrivesAt-Now>CycleEngine.Walk(c,v,task));
        Assert.InRange(Math.Abs((c.CycleEndsAt!.Value-c.Trip.ArrivesAt-work).TotalMilliseconds),0,1);
        Assert.Null(CycleEngine.Settle(c,new(){Village=v},c.Trip.ArrivesAt.AddMilliseconds(-1)));
    }

    [Fact]
    public void Dungeon_march_uses_a_reachable_western_exit_instead_of_a_blocked_fixed_tile()
    {
        var v=NewVillage();var c=NewCitizen(v);
        v.Fortifications.Add(new(){Id="closed-old-departure",X=(int)Math.Floor(DungeonRules.MarchSpot.X),Y=(int)Math.Floor(DungeonRules.MarchSpot.Y)});
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Tsar",1,Now);
        DungeonRules.Join(run,v,c,HeroClass.Brawler,Now);
        DungeonRules.March(run,v,[c],Now);
        Assert.Equal(DungeonPhase.Marching,run.Phase);Assert.Equal(v.TsarTrip!.To,c.Trip!.To);
        Assert.NotEqual(DungeonRules.MarchSpot,c.Trip.To);Assert.Equal(WorldMap.Land.X+.5,c.Trip.To.X);
        Walkable(v,c.Trip);Walkable(v,v.TsarTrip);
    }

    [Fact]
    public void A_gathering_that_loses_every_exit_cancels_and_returns_reserved_gear_instead_of_retrying_forever()
    {
        var v=NewVillage();var c=NewCitizen(v);v.Store(ResourceKind.Spear,1);
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Tsar",1,Now);
        DungeonRules.Join(run,v,c,HeroClass.Spearman,Now);Assert.Equal(0,v.Stock(ResourceKind.Spear));
        for(var y=WorldMap.Land.Y;y<WorldMap.Land.Y+WorldMap.Land.Height;y++)v.Fortifications.Add(new(){Id=$"closed-{y}",X=11,Y=y});
        DungeonRules.March(run,v,[c],Now);
        Assert.Equal(DungeonPhase.Ended,run.Phase);Assert.Equal(DungeonOutcome.Cancelled,run.Outcome);
        Assert.Null(c.DungeonRunId);Assert.Equal(1,v.Stock(ResourceKind.Spear));Assert.Null(c.Trip);
        Assert.Equal("spot.noPath",Assert.Throws<GameRuleException>(()=>DungeonRules.Open(v,DungeonCatalog.DeadLands,"Tsar",2,Now)).Code);
    }

    [Fact]
    public void Earned_dungeon_loot_settles_once_when_the_road_home_is_closed_and_return_waits_at_the_real_exit()
    {
        var v=NewVillage();var c=NewCitizen(v);
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Tsar",1,Now);
        DungeonRules.Join(run,v,c,HeroClass.Brawler,Now);DungeonRules.March(run,v,[c],Now);
        var at=v.TsarTrip!.ArrivesAt+TimeSpan.FromMinutes(1);var exit=c.Trip!.To;
        for(var y=WorldMap.Land.Y;y<WorldMap.Land.Y+WorldMap.Land.Height;y++)v.Fortifications.Add(new(){Id=$"closed-{y}",X=11,Y=y});
        var outcome=new DungeonHeroOutcome(1,10,[new(ResourceKind.Wood,1)],1,0,false,false,null);
        var before=v.Stock(ResourceKind.Wood)+c.Carried(ResourceKind.Wood);
        DungeonRules.Settle(run,run.Hero(c.Id)!,c,v,outcome,new Random(1),at);
        DungeonRules.Settle(run,run.Hero(c.Id)!,c,v,outcome,new Random(1),at);
        DungeonRules.Settle(run,run.Tsar,null,v,outcome,new Random(1),at);
        Assert.Equal(before+1,v.Stock(ResourceKind.Wood)+c.Carried(ResourceKind.Wood));
        foreach(var trip in new[]{c.Trip!,v.TsarTrip!})
        {
            Assert.True(trip.Waiting(at));Assert.Equal("path",trip.BlockReason);Assert.Equal(WorldMap.Places[WorldMap.Camp],trip.To);
            Assert.Equal(exit,trip.PositionAt(at));Assert.Equal(exit,trip.PositionAt(at.AddDays(1)));Assert.Single(trip.Waypoints);
        }
        var identity=c.Trip!.Id;v.Fortifications.Clear();
        RegionalTravel.Resume(c,v,at.AddMinutes(1));Assert.Equal(identity,c.Trip!.Id);Assert.Equal(exit,c.Trip.From);
        Assert.Equal(WorldMap.Places[WorldMap.Camp],c.Trip.To);Assert.Null(c.Trip.BlockedAt);Walkable(v,c.Trip);
    }

    [Fact]
    public void A_late_hero_stays_in_the_world_and_returns_from_the_actual_walk_position_instead_of_spawning_in_battle()
    {
        var v=NewVillage();var c=NewCitizen(v);c.IdleSpot=RegionalTerrain.MarshTrail;v.TsarSpot=RegionalTravel.WesternExit(v);v.Store(ResourceKind.Spear,1);
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Tsar",1,Now);
        DungeonRules.Join(run,v,c,HeroClass.Spearman,Now);DungeonRules.March(run,v,[c],Now);
        var at=run.MarchEndsAt!.Value;Assert.True(c.Trip!.ArrivesAt>at);var actual=c.Trip.PositionAt(at);
        Assert.True(DungeonRules.CompleteMarch(run,v,[c],at));
        Assert.Null(run.Hero(c.Id));Assert.Null(c.DungeonRunId);Assert.Equal(1,v.Stock(ResourceKind.Spear));
        Assert.Equal(actual,c.Trip!.From);Assert.Equal(WorldMap.Places[WorldMap.Camp],c.Trip.To);
    }

    [Fact]
    public void A_gate_closed_during_the_march_does_not_teleport_blocked_heroes_into_the_dungeon()
    {
        var v=NewVillage();var c=NewCitizen(v);
        for(var y=WorldMap.Land.Y;y<WorldMap.Land.Y+WorldMap.Land.Height;y++)v.Fortifications.Add(new(){Id=$"march-{y}",X=11,Y=y,Gate=y==13,Open=y==13});
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Tsar",1,Now);
        DungeonRules.Join(run,v,c,HeroClass.Brawler,Now);DungeonRules.March(run,v,[c],Now);
        v.Fortifications.Single(f=>f.Gate).Open=false;
        Assert.True(RegionalTravel.Reconcile(v,c.Trip!,Now.AddSeconds(.1)));Assert.True(RegionalTravel.Reconcile(v,v.TsarTrip!,Now.AddSeconds(.1)));
        var at=run.MarchEndsAt!.Value;var actual=c.Trip!.PositionAt(at);
        Assert.False(DungeonRules.CompleteMarch(run,v,[c],at));Assert.Equal(DungeonPhase.Ended,run.Phase);
        Assert.Equal(DungeonOutcome.Abandoned,run.Outcome);Assert.Null(c.DungeonRunId);Assert.Equal(actual,c.Trip!.From);
        Assert.Equal(WorldMap.Places[WorldMap.Camp],c.Trip.To);
    }

}
