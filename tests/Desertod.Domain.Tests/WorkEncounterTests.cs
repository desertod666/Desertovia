using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class WorkEncounterTests
{
    private sealed class NoBreakRandom:Random{public override double NextDouble()=>.999;}
    private static (Village World,Citizen Person,WorkContext Work,DungeonRun Run,DateTimeOffset Due) Setup()
    {
        var world=NewVillage();var person=NewCitizen(world);var work=WorkContext.For(world);
        CycleEngine.Start(person,GameCatalog.FindTask("hunt")!,work,Now);
        person.CycleEncounterVersion=1;person.CycleAmbushChecked=true;
        var due=person.CycleEndsAt!.Value;
        var run=WorkEncounterRules.Open(person,EnemyKind.Wolf,72,due);
        return(world,person,work,run,due);
    }
    private static DungeonHeroOutcome Outcome(bool won,bool knocked=false,int arrows=0)=>new(won?1:0,0,[],won?1:0,arrows,false,knocked,knocked?EnemyKind.Wolf:null);

    [Fact]
    public void Accepted_work_is_frozen_and_cannot_be_cancelled_or_replaced_by_walking()
    {
        var s=Setup();var energy=s.Person.StoredEnergy;var started=s.Person.CycleStartedAt;
        Assert.Equal(WorkState.Paused,s.Person.WorkState(s.Due.AddHours(1)));
        Assert.Null(CycleEngine.Settle(s.Person,s.Work,s.Due.AddHours(1)));Assert.False(s.Person.CycleSettled);
        Assert.Equal("ambush.retreatRequired",Assert.Throws<GameRuleException>(()=>CycleEngine.Cancel(s.Person,s.Due,s.World)).Code);
        Assert.Equal("goto.working",Assert.Throws<GameRuleException>(()=>s.Person.GoTo(new(15.5,13.5),s.World,s.Due)).Code);
        Assert.Equal(energy,s.Person.StoredEnergy);Assert.Equal(started,s.Person.CycleStartedAt);
    }

    [Fact]
    public void Victory_uses_one_work_result_and_duplicate_settlement_cannot_pay_loot_or_xp_again()
    {
        var s=Setup();var energy=s.Person.StoredEnergy;
        var result=WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(true),new NoBreakRandom(),s.Due);
        Assert.True(result.Won);Assert.Equal(EnemyKind.Wolf,WorkEncounterRules.Report(s.Person)!.Enemy);
        Assert.Equal(result.Enemy,WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(false),new NoBreakRandom(),s.Due).Enemy);
        s.Work.SkipAmbush=true;s.Work.ResolvedAmbush=WorkEncounterRules.Report(s.Person);
        var report=CycleEngine.Settle(s.Person,s.Work,s.Due)!;Assert.True(report.Fight!.Won);
        Assert.Contains(report.Produced,p=>p.Resource==ResourceKind.Food);Assert.Contains(report.Produced,p=>p.Resource==ResourceKind.Leather);
        var stock=s.World.Stock(ResourceKind.Leather);var skills=JsonSerializer.Serialize(s.Person.Skills);
        Assert.Null(CycleEngine.Settle(s.Person,s.Work,s.Due.AddHours(1)));Assert.Equal(stock,s.World.Stock(ResourceKind.Leather));
        Assert.Equal(skills,JsonSerializer.Serialize(s.Person.Skills));Assert.Equal(energy,s.Person.StoredEnergy);
    }

    [Fact]
    public void Voluntary_escape_grants_no_work_output_or_new_wound_and_finishes_requested_rest()
    {
        var s=Setup();SafeRest.Request(s.Person,s.Due);
        var result=WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(false),new NoBreakRandom(),s.Due);
        Assert.False(result.Won);Assert.False(result.Injured);Assert.Empty(result.Loot);
        s.Work.SkipAmbush=true;s.Work.ResolvedAmbush=result;
        var report=CycleEngine.Settle(s.Person,s.Work,s.Due)!;
        Assert.Empty(report.Produced);Assert.True(report.Overflowed);Assert.Equal(RestState.Resting,s.Person.RestState);
        Assert.Null(s.Person.InjuredUntil);
    }

    [Fact]
    public void Simulated_arrows_are_spent_exactly_once_without_the_instant_fight_extra_shot()
    {
        var s=Setup();s.Person.Weapon=ResourceKind.Bow;s.Person.Arrows=10;
        WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(true,arrows:7),new NoBreakRandom(),s.Due);
        Assert.Equal(7,s.Person.Arrows);
        WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(true,arrows:0),new NoBreakRandom(),s.Due);
        Assert.Equal(7,s.Person.Arrows);
    }

    [Fact]
    public void Recovery_clock_adjustment_does_not_turn_the_same_accepted_work_into_a_different_cycle()
    {
        var s=Setup();var original=s.Run.WorkStartedAt;s.Person.CycleStartedAt+=TimeSpan.FromHours(1);
        var report=WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(false),new NoBreakRandom(),s.Due.AddHours(1));
        Assert.False(report.Won);Assert.Equal(original,s.Run.WorkStartedAt);Assert.Null(s.Person.DungeonRunId);
    }

    [Fact]
    public void A_previous_encounter_cannot_settle_a_new_cycle()
    {
        var s=Setup();WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(false),new NoBreakRandom(),s.Due);
        s.Work.SkipAmbush=true;s.Work.ResolvedAmbush=WorkEncounterRules.Report(s.Person);CycleEngine.Settle(s.Person,s.Work,s.Due);
        CycleEngine.Start(s.Person,GameCatalog.FindTask("forage")!,s.Work,s.Due.AddSeconds(1));
        Assert.Equal("ambush.changed",Assert.Throws<GameRuleException>(()=>WorkEncounterRules.Settle(s.Run,s.Run.Heroes[0],s.Person,Outcome(true),new NoBreakRandom(),s.Due.AddSeconds(1))).Code);
        Assert.Null(s.Person.CycleEncounterRunId);Assert.Null(s.Person.CycleEncounterReportJson);
    }
}
