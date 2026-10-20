using Desertod.Domain.Catalog;

namespace Desertod.Domain.Economy;

/// <summary>Exact allocation by actual output quantity × recipe base unit price. No market quote or tax activation.</summary>
public static class ProductionCost
{
    public static IReadOnlyDictionary<ResourceKind, long> Allocate(long basis, IReadOnlyList<ResourceAmount> outputs)
    {
        if (basis < 0) throw new ArgumentOutOfRangeException(nameof(basis));
        var weights = outputs.Where(o => o.Amount > 0).GroupBy(o => o.Resource)
            .Select(g => (Kind: g.Key, Weight: g.Sum(o => (long)o.Amount) * EconomyRules.StorePrice(g.Key)))
            .OrderBy(g => g.Kind).ToArray();
        if (weights.Length == 0) return new Dictionary<ResourceKind, long>();
        var total = weights.Sum(w => w.Weight);
        var pieces = weights.Select(w => (w.Kind, Basis: (long)((Int128)basis * w.Weight / total), Remainder: (Int128)basis * w.Weight % total)).ToArray();
        var result = pieces.ToDictionary(p => p.Kind, p => p.Basis);
        var remainder = basis - result.Values.Sum();
        foreach (var piece in pieces.OrderByDescending(p => p.Remainder).ThenBy(p => p.Kind).Take((int)remainder)) result[piece.Kind]++;
        return result;
    }
}
