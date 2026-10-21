using Desertod.Domain;
using Desertod.Domain.Director;

namespace Desertod.Domain.Tests;

public sealed class DirectorFinanceTests
{
    private static DirectorFinanceLimits Limits => new() { Enabled = true, WorkOrders = true, ActionsPerDay = 2,
        SpendingPerDay = 100, MaximumUnitPay = 20, MaximumUnits = 5 };

    [Theory]
    [InlineData(0, 100)] [InlineData(97, 100)] [InlineData(2, -1)]
    public void Enabled_delegation_requires_explicit_bounded_allowances(int actions, long spending) =>
        Assert.Throws<GameRuleException>(() => DirectorFinanceRules.Validate(Limits with { ActionsPerDay = actions, SpendingPerDay = spending }));

    [Fact]
    public void A_new_policy_does_not_refill_day_spending_and_rejection_does_not_mutate_counter()
    {
        var day = new DirectorFinanceDay(); DirectorFinanceRules.Reserve(Limits, day, 70);
        Assert.Throws<GameRuleException>(() => DirectorFinanceRules.Reserve(Limits with { SpendingPerDay = 80 }, day, 20));
        Assert.Equal(70, day.Reserved); Assert.Equal(1, day.Actions); Assert.Equal(1, day.Revision);
        DirectorFinanceRules.Reserve(Limits, day, 30);
        Assert.Throws<GameRuleException>(() => DirectorFinanceRules.Reserve(Limits, day, 0));
        Assert.Equal(100, day.Reserved); Assert.Equal(2, day.Actions);
    }

    [Fact]
    public void Economic_capability_has_closed_ids_and_other_events_keep_owner_approval()
    {
        var permissions = AlmightyPermissions.Describe([new("finance_work_low", "Work", "Backed"), new("rest", "Rest", "Two hours")]);
        Assert.True(permissions[0].Autonomous); Assert.False(permissions[0].RequiresOwnerApproval);
        Assert.False(permissions[1].Autonomous); Assert.True(permissions[1].RequiresOwnerApproval);
        Assert.Throws<ArgumentException>(() => AlmightyPermissions.Describe([new("finance_take_wallet", "Bad", "Bad")]));
        Assert.False(AlmightyPermissions.IsEnabled("finance_work_low", ["economy"]));
    }
}
