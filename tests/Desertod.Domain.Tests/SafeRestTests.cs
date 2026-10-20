using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Rules;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class SafeRestTests
{
    [Fact]
    public void A_new_encounter_during_accepted_work_delays_protection_until_retreat()
    {
        var v = NewVillage(); var c = NewCitizen(v); var context = WorkContext.For(v);
        CycleEngine.Start(c, Task("chop-wood"), context, Now);
        SafeRest.Request(c, Now);
        Assert.True(RaidSimulation.CanFight(c, Now));
        context.RestBlockedByCombat = true;
        Assert.NotNull(CycleEngine.Settle(c, context, c.CycleEndsAt!.Value));
        Assert.Equal(RestState.RestRequested, c.RestState);
        context.RestBlockedByCombat = false;
        Assert.Null(CycleEngine.Settle(c, context, c.CycleEndsAt!.Value.AddSeconds(1)));
        Assert.Equal(RestState.Resting, c.RestState);
    }
    [Fact]
    public void Accepted_work_finishes_once_before_rest_and_rest_produces_no_more_work_or_experience()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var context = WorkContext.For(village);
        CycleEngine.Start(citizen, Task("chop-wood"), context, Now);
        SafeRest.Request(citizen, Now.AddSeconds(1));
        var end = citizen.CycleEndsAt!.Value;
        Assert.Equal(RestState.RestRequested, citizen.RestState);
        Assert.Null(CycleEngine.Settle(citizen, context, end.AddSeconds(-1)));
        var report = CycleEngine.Settle(citizen, context, end);
        Assert.NotNull(report);
        Assert.True(report.PrimaryExperience > 0);
        Assert.Equal(RestState.Resting, citizen.RestState);
        var experience = citizen.Stats.GetExperience(report.Primary);
        var stock = village.Stock(report.Produced[0].Resource);
        Assert.Null(CycleEngine.Settle(citizen, context, end.AddDays(2)));
        Assert.Equal(experience, citizen.Stats.GetExperience(report.Primary));
        Assert.Equal(stock, village.Stock(report.Produced[0].Resource));
        Assert.Equal("rest.activeRequired", Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("chop-wood"), context, end.AddDays(2))).Code);
        Assert.False(RaidSimulation.CanFight(citizen, end));
        Assert.False(RaidRules.AtRest(citizen, end));
        SafeRest.Wake(citizen);
        CycleEngine.Start(citizen, Task("chop-wood"), context, end.AddDays(2));
        Assert.Equal(RestState.Active, citizen.RestState);
    }

    [Fact]
    public void Pending_rest_cannot_cancel_paid_work_but_waking_can_withdraw_the_rest_request()
    {
        var village = NewVillage(); var citizen = NewCitizen(village); var context = WorkContext.For(village);
        CycleEngine.Start(citizen, Task("chop-wood"), context, Now);
        var end = citizen.CycleEndsAt;
        SafeRest.Request(citizen, Now);
        SafeRest.Request(citizen, Now.AddMinutes(1));
        Assert.Equal(Now, citizen.RestRequestedAt);
        Assert.Equal("rest.activeRequired", Assert.Throws<GameRuleException>(() => CycleEngine.Cancel(citizen, Now, village)).Code);
        SafeRest.Wake(citizen);
        Assert.Equal(end, citizen.CycleEndsAt);
        Assert.False(citizen.CycleSettled);
        Assert.Null(citizen.RestRequestedAt);
    }

    [Fact]
    public void Idle_is_not_safe_rest_and_both_political_roles_must_wake_before_physical_actions()
    {
        var village = NewVillage(); var citizen = NewCitizen(village);
        Assert.True(RaidSimulation.CanFight(citizen, Now));
        SafeRest.Request(citizen, Now);
        Assert.Equal(RestState.Resting, citizen.RestState);
        Assert.Equal("rest.activeRequired", Assert.Throws<GameRuleException>(() => citizen.GoTo(new(15, 15), village, Now)).Code);
        village.TsarRestState = RestState.Resting;
        Assert.Equal("rest.activeRequired", Assert.Throws<GameRuleException>(() => village.TsarGoTo(new(15, 15), Now)).Code);
        Assert.Equal("rest.activeRequired", Assert.Throws<GameRuleException>(() => DungeonRules.Open(village, "any", "Tsar", 1, Now)).Code);
    }
}
