using Desertod.Domain.Notifications;
using Desertod.Domain.Catalog;
using Desertod.Domain.Rules;

namespace Desertod.Domain.Tests;

public sealed class NotificationPreferenceTests
{
    [Fact]
    public void Quiet_hours_follow_local_midnight_and_both_DST_occurrences()
    {
        var settings=new NotificationPreference{QuietEnabled=true,TimeZoneId="Europe/Oslo"};
        Assert.True(NotificationPreferenceRules.IsQuiet(settings,DateTimeOffset.Parse("2026-07-01T20:00:00Z")));
        Assert.False(NotificationPreferenceRules.IsQuiet(settings,DateTimeOffset.Parse("2026-07-02T06:00:00Z")));
        settings.QuietStartMinute=60;settings.QuietEndMinute=180;
        Assert.True(NotificationPreferenceRules.IsQuiet(settings,DateTimeOffset.Parse("2026-10-25T00:30:00Z")));
        Assert.True(NotificationPreferenceRules.IsQuiet(settings,DateTimeOffset.Parse("2026-10-25T01:30:00Z")));
        Assert.False(NotificationPreferenceRules.IsQuiet(settings,DateTimeOffset.Parse("2026-10-25T02:00:00Z")));
        Assert.True(NotificationPreferenceRules.IsQuiet(settings,DateTimeOffset.Parse("2026-03-29T00:59:00Z")));
        Assert.False(NotificationPreferenceRules.IsQuiet(settings,DateTimeOffset.Parse("2026-03-29T01:00:00Z")));
    }

    [Fact]
    public void Default_is_opt_out_and_invalid_time_or_category_is_not_an_implicit_all_day_rule()
    {
        Assert.False(new NotificationPreference().BrowserEnabled);
        NotificationPreferenceRules.Validate(NotificationCategory.All,false,1320,480,"UTC");
        Assert.Throws<GameRuleException>(()=>NotificationPreferenceRules.Validate((NotificationCategory)128,false,0,1,"UTC"));
        Assert.Throws<GameRuleException>(()=>NotificationPreferenceRules.Validate(NotificationCategory.Work,true,60,60,"UTC"));
        Assert.Throws<GameRuleException>(()=>NotificationPreferenceRules.Validate(NotificationCategory.Work,true,-1,60,"UTC"));
        Assert.Throws<GameRuleException>(()=>NotificationPreferenceRules.Validate(NotificationCategory.Work,true,1,60,"not/a-zone"));
        Assert.Throws<GameRuleException>(()=>NotificationPreferenceRules.Validate(NotificationCategory.Work,true,1,60,"W. Europe Standard Time"));
    }

    [Fact]
    public void Work_event_carries_actual_cycle_identity_and_settlement_time_even_when_rest_clears_the_task()
    {
        var village=TestWorld.NewVillage();var citizen=TestWorld.NewCitizen(village);
        var start=TestWorld.Now;var context=new WorkContext{Village=village};
        CycleEngine.Start(citizen,GameCatalog.FindTask("chop-wood")!,context,start);
        var began=citizen.CycleStartedAt;var ended=citizen.CycleEndsAt!.Value;
        SafeRest.Request(citizen,start.AddSeconds(1));
        var report=CycleEngine.Settle(citizen,context,ended.AddMinutes(2));
        Assert.NotNull(report);Assert.Equal(began,report.CycleStartedAt);Assert.Equal(ended,report.CycleEndedAt);
        Assert.Equal(ended.AddMinutes(2),report.SettledAt);Assert.Null(CycleEngine.Settle(citizen,context,ended.AddMinutes(3)));
    }
}
