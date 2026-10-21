using Desertod.Domain;
using Desertod.Domain.Economy;

namespace Desertod.Domain.Tests;

public sealed class TreasuryFundingTests
{
    [Fact]
    public void Explicit_allowance_bounds_issuance_and_versions_do_not_reset_its_used_amount()
    {
        var policy = new TreasuryFundingPolicy { Enabled = true, DailyIssueLimit = 100 };
        var day = new TreasuryIssueDay();
        Assert.Equal(70, TreasuryFundingRules.Issue(policy, day, 60, 10));
        Assert.Equal(60, day.Issued); Assert.Equal(1, day.Revision);
        policy.Version++; policy.DailyIssueLimit = 50;
        Assert.Throws<GameRuleException>(() => TreasuryFundingRules.Issue(policy, day, 1, 70));
        Assert.Equal(60, day.Issued);
        policy.DailyIssueLimit = 100;
        Assert.Equal(110, TreasuryFundingRules.Issue(policy, day, 40, 70));
        Assert.Equal(100, day.Issued); Assert.Equal(2, day.Revision);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    [InlineData(true, EconomyRules.MaximumBalance + 1)]
    public void No_implicit_budget_or_nonzero_disabled_policy(bool enabled, long limit) =>
        Assert.Throws<GameRuleException>(() => TreasuryFundingRules.ValidatePolicy(enabled, limit));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(101, 0, 0)]
    [InlineData(1, 100, 0)]
    [InlineData(1, 0, EconomyRules.MaximumBalance)]
    [InlineData(1, 0, -1)]
    public void Invalid_or_overdrawn_issue_never_mutates_the_allowance(long amount, long alreadyIssued, long treasury)
    {
        var day = new TreasuryIssueDay { Issued = alreadyIssued, Revision = 9 };
        Assert.Throws<GameRuleException>(() => TreasuryFundingRules.Issue(new() { Enabled = true, DailyIssueLimit = 100 }, day, amount, treasury));
        Assert.Equal(alreadyIssued, day.Issued); Assert.Equal(9, day.Revision);
    }

    [Fact]
    public void Another_world_cannot_spend_the_allowance_and_disabled_state_never_issues()
    {
        var policy = new TreasuryFundingPolicy { Enabled = true, DailyIssueLimit = 100, VillageId = Guid.NewGuid() };
        Assert.Throws<InvalidOperationException>(() => TreasuryFundingRules.Issue(policy, new(), 1, 0));
        policy.Enabled = false; policy.DailyIssueLimit = 0;
        Assert.Throws<GameRuleException>(() => TreasuryFundingRules.Issue(policy, new() { VillageId = policy.VillageId }, 1, 0));
    }

    [Fact]
    public void Treasury_issue_has_a_separate_audit_kind_and_cannot_credit_a_private_wallet()
    {
        var position = new MonetaryPosition("treasury", null);
        var leg = new LedgerEntry { OperationId = Guid.NewGuid(), Operation = "treasury-issuance", Asset = "DZK", Location = "treasury", Delta = 10 };
        var result = Assert.Single(MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long>(), new Dictionary<MonetaryPosition, long> { [position] = 10 }, [leg]));
        Assert.Equal("controlled-treasury-issuance", result.Kind); Assert.Equal(10, result.Issued);
        leg.Location = "wallet"; leg.AccountId = Guid.NewGuid();
        Assert.Throws<GameRuleException>(() => MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long>(),
            new Dictionary<MonetaryPosition, long> { [new("wallet", leg.AccountId)] = 10 }, [leg]));
    }
}
