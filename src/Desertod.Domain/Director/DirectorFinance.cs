using Desertod.Domain.Economy;

namespace Desertod.Domain.Director;

/// <summary>Explicit delegation ceilings, never a default economic balance or a source of money.</summary>
public sealed record DirectorFinanceLimits
{
    public bool Enabled { get; init; }
    public bool WorkOrders { get; init; }
    public bool SupplyOrders { get; init; }
    public bool TaxChanges { get; init; }
    public bool TreasuryIssues { get; init; }
    public int ActionsPerDay { get; init; }
    public long SpendingPerDay { get; init; }
    public long MaximumUnitPay { get; init; }
    public int MaximumUnits { get; init; }
    public int MaximumTaxBasisPoints { get; init; }
    public int TaxStepBasisPoints { get; init; }
    public int TaxNoticeMinutes { get; init; }
    public int TaxCooldownMinutes { get; init; }
    public long MaximumIssue { get; init; }
}

public sealed class DirectorFinancePolicy
{
    public Guid VillageId { get; set; }
    public long Version { get; set; }
    public Guid RequestId { get; set; }
    public Guid OwnerAccountId { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public DirectorFinanceLimits Limits { get; set; } = new();
}

public sealed class DirectorFinanceDay
{
    public Guid VillageId { get; set; }
    public DateOnly Day { get; set; }
    public int Actions { get; set; }
    public long Reserved { get; set; }
    public long Revision { get; set; }
}

public sealed class DirectorFinanceAction
{
    public Guid VillageId { get; set; }
    public Guid ReviewId { get; set; }
    public long PolicyVersion { get; set; }
    public DateOnly Day { get; set; }
    public Guid DelegatingOwnerId { get; set; }
    public string Author { get; set; } = "director";
    public bool Applied { get; set; } = true;
    public string Option { get; set; } = "";
    public string Terms { get; set; } = "{}";
    public string Reason { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Before { get; set; } = "{}";
    public string Result { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public static class DirectorFinanceRules
{
    public static bool IsOption(string id) => id is "finance_tax_up" or "finance_tax_down" or "finance_issue"
        or "finance_work_low" or "finance_work_mid" or "finance_work_high"
        or "finance_supply_low" or "finance_supply_mid" or "finance_supply_high";

    public static void Validate(DirectorFinanceLimits limits)
    {
        if (!limits.Enabled) return;
        if (limits.ActionsPerDay is < 1 or > AlmightyPolicy.DailyModelLimit
            || limits.SpendingPerDay is < 0 or > EconomyRules.MaximumBalance
            || limits.MaximumUnitPay is < 0 or > 1000 || limits.MaximumUnits is < 0 or > 1000
            || (limits.WorkOrders || limits.SupplyOrders) && (limits.MaximumUnitPay == 0 || limits.MaximumUnits == 0 || limits.SpendingPerDay == 0)
            || limits.MaximumTaxBasisPoints is < 0 or > 10000
            || limits.TaxChanges && (limits.TaxStepBasisPoints < 1 || limits.TaxStepBasisPoints > limits.MaximumTaxBasisPoints
                || limits.TaxNoticeMinutes is < 1 or > 10080 || limits.TaxCooldownMinutes is < 15 or > 10080)
            || limits.MaximumIssue is < 0 or > EconomyRules.MaximumBalance || limits.TreasuryIssues && limits.MaximumIssue == 0)
            throw new GameRuleException("director.finance.limits", "Specify explicit economic delegation ceilings.");
    }

    public static void Reserve(DirectorFinanceLimits limits, DirectorFinanceDay day, long amount)
    {
        Validate(limits);
        if (!limits.Enabled || day.Actions >= limits.ActionsPerDay || amount < 0 || amount > limits.SpendingPerDay - day.Reserved
            || day.Revision == long.MaxValue)
            throw new GameRuleException("director.finance.budget", "The delegated action or spending allowance is exhausted.");
        day.Actions++; day.Reserved += amount; day.Revision++;
    }
}
