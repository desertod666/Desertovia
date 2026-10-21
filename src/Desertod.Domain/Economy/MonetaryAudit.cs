namespace Desertod.Domain.Economy;

/// <summary>A monetary position. Escrows and the treasury belong to the world, not the initiating actor.</summary>
public readonly record struct MonetaryPosition(string Location, Guid? AccountId)
{
    public static MonetaryPosition From(string location, Guid? account)
    {
        if (location == "wallet" && account is not null) return new(location, account);
        if (location == "treasury" || IsFund(location, "work-reserve:") || IsFund(location, "supply-order:"))
            return new(location, null);
        throw MonetaryAudit.Invalid();
    }

    private static bool IsFund(string location, string prefix) => location.StartsWith(prefix, StringComparison.Ordinal)
        && Guid.TryParseExact(location[prefix.Length..], "D", out _);
}

public sealed class MonetaryOperation
{
    public Guid OperationId { get; set; }
    public string Kind { get; set; } = "transfer";
    public decimal Issued { get; set; }
    public decimal Withdrawn { get; set; }
    public decimal Transferred { get; set; }
}

/// <summary>Finalized in the same transaction as its postings and balance changes. Historical money is an opening anchor.</summary>
public sealed class MonetaryBatch
{
    public Guid VillageId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset At { get; set; }
    public bool Finalized { get; set; }
    public decimal BeforeTotal { get; set; }
    public decimal AfterTotal { get; set; }
    public decimal Issued { get; set; }
    public decimal Withdrawn { get; set; }
    public decimal Transferred { get; set; }
    public int PostingCount { get; set; }
    public string BeforeHash { get; set; } = "";
    public string AfterHash { get; set; } = "";
    public string PostingHash { get; set; } = "";
    public List<MonetaryOperation> Operations { get; set; } = [];
}

public static class MonetaryAudit
{
    // Existing game executors. Adding a new monetary operation requires an explicit classification.
    private static readonly HashSet<string> Transfers = ["trade", "sale-tax", "buy-stock", "fund-treasury",
        "work-reserve", "work-refund", "contract-wage", "supply-reserve", "supply-refund", "supply-payment"];

    public static GameRuleException Invalid() => new("economy.reconciliation", "Money changes do not match the transaction journal.");

    public static IReadOnlyList<MonetaryOperation> Reconcile(IReadOnlyDictionary<MonetaryPosition, long> before,
        IReadOnlyDictionary<MonetaryPosition, long> after, IReadOnlyList<LedgerEntry> postings)
    {
        if (before.Values.Any(x => x < 0) || after.Values.Any(x => x < 0)
            || postings.Any(x => x.Asset != "DZK" || x.Delta == 0)) throw Invalid();
        var byPosition = postings.GroupBy(x => MonetaryPosition.From(x.Location, x.AccountId))
            .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.Delta));
        foreach (var key in before.Keys.Union(after.Keys).Union(byPosition.Keys))
            if ((decimal)after.GetValueOrDefault(key) - before.GetValueOrDefault(key) != byPosition.GetValueOrDefault(key))
                throw Invalid();
        var operations = new List<MonetaryOperation>();
        foreach (var group in postings.GroupBy(x => x.OperationId))
        {
            var legs = group.ToArray(); var credits = legs.Where(x => x.Delta > 0).Sum(x => (decimal)x.Delta);
            var debits = legs.Where(x => x.Delta < 0).Sum(x => -(decimal)x.Delta);
            var net = credits - debits;
            if (net == 0 && legs.All(x => Transfers.Contains(x.Operation)))
                operations.Add(new() { OperationId = group.Key, Transferred = credits });
            else if (legs.Length == 1 && legs[0] is { Operation: "wage", Location: "wallet", AccountId: not null, Delta: >= 1 and <= 1000 })
                operations.Add(new() { OperationId = group.Key, Kind = "legacy-wage-issuance", Issued = net });
            else if (legs.Length == 1 && legs[0] is { Location: "wallet", AccountId: not null, Delta: < 0 }
                && (legs[0].Operation == "energy-drink" && legs[0].Delta == -300
                    || legs[0].Operation == "exchange-burn" && -((decimal)legs[0].Delta) <= 2_000_000))
                operations.Add(new() { OperationId = group.Key, Kind = "withdrawal", Withdrawn = -net });
            else throw Invalid();
        }
        return operations;
    }
}
