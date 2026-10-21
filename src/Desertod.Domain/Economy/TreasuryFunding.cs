namespace Desertod.Domain.Economy;

/// <summary>Explicit owner authority ceiling, not a default rate or an instruction to issue the whole allowance.</summary>
public sealed class TreasuryFundingPolicy
{
    public Guid VillageId { get; set; }
    public long Version { get; set; }
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string RequestFingerprint { get; set; } = "";
    public bool Enabled { get; set; }
    public long DailyIssueLimit { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
}

/// <summary>Shared across policy versions. UTC-day rollover creates a new allowance, not new money.</summary>
public sealed class TreasuryIssueDay
{
    public Guid VillageId { get; set; }
    public DateOnly Day { get; set; }
    public long Issued { get; set; }
    public long Revision { get; set; }
}

public sealed class TreasuryIssue
{
    public Guid VillageId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string RequestFingerprint { get; set; } = "";
    public long PolicyVersion { get; set; }
    public Guid FinancialBatchId { get; set; }
    public DateOnly Day { get; set; }
    public long Amount { get; set; }
    public long IssuedTotalAfter { get; set; }
    public long RemainingAtIssue { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class TreasuryFundingRules
{
    // Existing monetary storage bound. Product limits must be explicitly published; zero means disabled.
    public static void ValidatePolicy(bool enabled, long dailyLimit)
    {
        if (enabled ? dailyLimit is < 1 or > EconomyRules.MaximumBalance : dailyLimit != 0)
            throw new GameRuleException("funding.terms", "Publish an explicit positive issue limit, or zero when disabled.");
    }

    public static long Issue(TreasuryFundingPolicy policy, TreasuryIssueDay day, long amount, long treasury)
    {
        ValidatePolicy(policy.Enabled, policy.DailyIssueLimit);
        if (policy.VillageId != day.VillageId) throw new InvalidOperationException("Issuance allowance belongs to another world.");
        if (!policy.Enabled) throw new GameRuleException("funding.disabled", "New treasury issuance is disabled.");
        if (amount < 1 || amount > policy.DailyIssueLimit || day.Issued < 0 || day.Issued > policy.DailyIssueLimit - amount)
            throw new GameRuleException("funding.allowance", "The remaining daily issue allowance is insufficient.");
        if (treasury < 0 || treasury > EconomyRules.MaximumBalance - amount || day.Revision == long.MaxValue)
            throw new GameRuleException("funding.limit", "The monetary limit has been reached.");
        day.Issued += amount; day.Revision++;
        return treasury + amount;
    }
}
