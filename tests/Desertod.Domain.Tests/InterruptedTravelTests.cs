using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class InterruptedTravelTests
{
    private static Trip Road(TilePoint? from=null,TilePoint? to=null)
    {
        var target=to??new TilePoint(20.5,13.5);var trip=Trip.Start(from??new(1.5,13.5),target,Now);
        trip.Waypoints.Add(new(){X=target.X,Y=target.Y});trip.RouteEndsAt=trip.ArrivesAt;return trip;
    }
    private static Fortification Gate(int x=4)=>new(){Id="fixture-gate",Gate=true,Open=false,X=x,Y=13};

    [Fact]
    public void Gate_ahead_stops_at_the_approaching_tile_and_freezes_position_and_fog()
    {
        var world=NewVillage();world.Explored=new byte[Exploration.Size];world.Fortifications.Add(Gate());var trip=Road();
        Assert.True(RegionalTravel.Reconcile(world,trip,Now.AddSeconds(.3)));Assert.Equal("gate",trip.BlockReason);
        Assert.InRange((trip.BlockedAt!.Value-Now).TotalSeconds,1.49,1.5);
        var stopped=trip.PositionAt(Now.AddHours(1));Assert.InRange(stopped.X,3.99,3.9999);Assert.Equal(13.5,stopped.Y);
        Assert.Equal(stopped,trip.PositionAt(Now.AddDays(1)));
        var seen=Exploration.Seen(world.Explored,[trip],Now.AddHours(1));Assert.False(Exploration.IsExplored(seen,20,13));
        Assert.False(RegionalTravel.Reconcile(world,trip,Now.AddHours(1)));
    }

    [Fact]
    public void Closing_a_gate_less_than_a_millisecond_ahead_cannot_rewind_the_walker()
    {
        var world=NewVillage();world.Fortifications.Add(Gate());var trip=Road();
        var changedAt=Now.AddSeconds(1.4995);var current=trip.PositionAt(changedAt);
        Assert.True(RegionalTravel.Reconcile(world,trip,changedAt));
        Assert.Equal(changedAt,trip.BlockedAt);Assert.Equal(current,trip.PositionAt(changedAt.AddMinutes(1)));
        Assert.True(current.X<4);
    }

    [Fact]
    public void A_gate_behind_the_walker_does_not_rewind_or_interrupt_the_journey()
    {
        var world=NewVillage();world.Fortifications.Add(Gate());var trip=Road();
        Assert.False(RegionalTravel.Reconcile(world,trip,Now.AddSeconds(3)));Assert.Null(trip.BlockedAt);
        Assert.Equal(trip.To,trip.PositionAt(trip.ArrivesAt));
    }

    [Fact]
    public void Reopening_before_arrival_removes_a_future_stop_but_reopening_after_stop_waits_for_explicit_resume()
    {
        var world=NewVillage();var gate=Gate();world.Fortifications.Add(gate);var trip=Road();
        RegionalTravel.Reconcile(world,trip,Now.AddSeconds(.3));gate.Open=true;
        Assert.True(RegionalTravel.Reconcile(world,trip,Now.AddSeconds(.8)));Assert.Null(trip.BlockedAt);
        gate.Open=false;RegionalTravel.Reconcile(world,trip,Now.AddSeconds(1));var stop=trip.BlockedAt!.Value;
        gate.Open=true;Assert.False(RegionalTravel.Reconcile(world,trip,stop.AddMinutes(5)));Assert.Equal(stop,trip.BlockedAt);
        var resumed=RegionalTravel.Resume(world,trip,stop.AddMinutes(5));Assert.Equal(trip.Id,resumed.Id);
        Assert.Equal(trip.PositionAt(stop),resumed.From);Assert.Null(resumed.BlockedAt);Assert.True(resumed.ArrivesAt>resumed.SetOutAt);
    }

    [Fact]
    public void A_building_placed_on_the_remaining_path_blocks_it_but_a_change_after_arrival_does_not_stop_work()
    {
        var world=NewVillage();world.Structures.Add(new(){Kind=Desertod.Domain.Buildings.BuildingKind.LeanTo,PlotId="7,12",BuiltAt=Now});
        var trip=Road();Assert.True(RegionalTravel.Reconcile(world,trip,Now.AddSeconds(.3)));Assert.Equal("path",trip.BlockReason);
        Assert.True(trip.PositionAt(Now.AddDays(1)).X<7);
        var arrived=Road();Assert.False(RegionalTravel.Reconcile(world,arrived,arrived.ArrivesAt.AddSeconds(1)));Assert.Null(arrived.BlockedAt);
    }

    [Fact]
    public void A_collapsed_bridge_blocks_crossing_but_does_not_trap_someone_already_on_the_far_bank()
    {
        var world=NewVillage();world.BridgeOpened=true;world.BridgeRuinedAt=Now.AddSeconds(.1);
        var crossing=Road(new(28.5,12.5),new(40.5,12.5));Assert.True(RegionalTravel.Reconcile(world,crossing,world.BridgeRuinedAt.Value));
        Assert.Equal("bridge",crossing.BlockReason);Assert.True(crossing.PositionAt(Now.AddMinutes(10)).X<30);
        Assert.Equal("bridge.closed",Assert.Throws<GameRuleException>(()=>RegionalTravel.Resume(world,crossing,Now.AddMinutes(1))).Code);
        var returned=RegionalTravel.SetOut(world,crossing,new(28.5,12.5),WorldMap.Places[WorldMap.Camp],Now.AddMinutes(1));
        Assert.Equal(crossing.PositionAt(Now.AddMinutes(1)),returned.From);
        var alreadyAcross=Road(new(28.5,12.5),new(40.5,12.5));Assert.False(RegionalTravel.Reconcile(world,alreadyAcross,Now.AddSeconds(5)));
        var local=RegionalTravel.SetOut(world,alreadyAcross,alreadyAcross.To,RegionalTerrain.QuarryTrail,Now.AddMinutes(1));
        Assert.NotEmpty(local.Waypoints);Assert.Equal(alreadyAcross.To,local.From);
    }

    [Fact]
    public void Accepted_work_cannot_pay_or_restart_while_waiting_and_resume_preserves_work_identity_and_energy()
    {
        var world=NewVillage();var citizen=NewCitizen(world);var task=GameCatalog.FindTask("cut-reeds")!;
        CycleEngine.Start(citizen,task,new(){Village=world},Now,new(8.5,94.5));var oldEnd=citizen.CycleEndsAt!.Value;
        var trip=citizen.Trip!;var remainingWork=oldEnd-trip.ArrivesAt;var mid=trip.PositionAt(Now+(trip.ArrivesAt-Now)/2);
        var gate=Gate((int)Math.Floor(mid.X));gate.Y=(int)Math.Floor(mid.Y);world.Fortifications.Add(gate);
        Assert.True(RegionalTravel.Reconcile(world,trip,Now.AddSeconds(.1)));
        var later=oldEnd.AddHours(1);var energy=citizen.StoredEnergy;var started=citizen.CycleStartedAt;
        Assert.Null(CycleEngine.Settle(citizen,new(){Village=world},later));Assert.False(citizen.CycleSettled);Assert.Equal(WorkState.Paused,citizen.WorkState(later));
        Assert.Equal("travel.blockedWork",Assert.Throws<GameRuleException>(()=>CycleEngine.Start(citizen,task,new(){Village=world},later)).Code);
        Assert.Equal("goto.working",Assert.Throws<GameRuleException>(()=>citizen.GoTo(WorldMap.Places[WorldMap.Camp],world,later)).Code);
        gate.Open=true;var resumed=RegionalTravel.Resume(citizen,world,later);
        Assert.Equal(started,citizen.CycleStartedAt);Assert.Equal(energy,citizen.StoredEnergy);Assert.Equal(resumed.ArrivesAt+remainingWork,citizen.CycleEndsAt);
        Assert.Null(CycleEngine.Settle(citizen,new(){Village=world},later));
        Assert.NotNull(CycleEngine.Settle(citizen,new(){Village=world},citizen.CycleEndsAt!.Value));
        Assert.Null(CycleEngine.Settle(citizen,new(){Village=world},citizen.CycleEndsAt!.Value.AddMinutes(1)));
    }

    [Fact]
    public void Cancelling_blocked_work_stops_at_the_current_point_without_completing_it()
    {
        var world=NewVillage();var citizen=NewCitizen(world);var task=GameCatalog.FindTask("cut-reeds")!;
        CycleEngine.Start(citizen,task,new(){Village=world},Now,new(8.5,94.5));var trip=citizen.Trip!;
        trip.BlockedAt=Now.AddSeconds(2);trip.BlockReason="path";var at=citizen.CycleEndsAt!.Value.AddMinutes(5);var stopped=trip.PositionAt(at);
        CycleEngine.Cancel(citizen,at,world);Assert.True(citizen.CycleSettled);Assert.Null(citizen.Trip);Assert.Null(citizen.CurrentTaskId);Assert.Equal(stopped,citizen.IdleSpot);
        Assert.Null(CycleEngine.Settle(citizen,new(){Village=world},at));
    }
}
