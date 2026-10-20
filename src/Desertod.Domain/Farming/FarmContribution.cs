using System.Numerics;

namespace Desertod.Domain.Farming;

public enum FarmStep { Sow, Water, Fertilize, Harvest }

/// <summary>One completed useful action on one crop, identified by accepted work rather than clicks.</summary>
public sealed class FarmContribution
{
    public Guid AccountId { get; set; }
    public required string Name { get; set; }
    public FarmStep Step { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}

public static class FarmContributionRules
{
    public const int Version = 1;
    /// <summary>Fractional Food and money claims use integer units; this does not create physical goods.</summary>
    public const long UnitsPerWhole = 1_000_000;

    private static int Weight(FarmStep step) => step switch
    {
        FarmStep.Sow=>40, FarmStep.Water=>30, FarmStep.Fertilize=>10, FarmStep.Harvest=>20,
        _=>throw new ArgumentOutOfRangeException(nameof(step))
    };

    /// <summary>
    /// Normalize stages that actually happened. All useful waters share30, however many were needed.
    /// Exact rational weights and deterministic largest remainders conserve even tiny/large funds.
    /// </summary>
    public static IReadOnlyDictionary<Guid,long> Allocate(long units,IEnumerable<FarmContribution> contributions)
    {
        if(units<0)throw new ArgumentOutOfRangeException(nameof(units));
        var stages=contributions.DistinctBy(c=>(c.AccountId,c.Step,c.StartedAt)).GroupBy(c=>c.Step).ToArray();
        if(stages.Length==0)return new Dictionary<Guid,long>();
        BigInteger common=1;
        foreach(var stage in stages)common=common/BigInteger.GreatestCommonDivisor(common,stage.Count())*stage.Count();
        var weights=new Dictionary<Guid,BigInteger>();
        foreach(var stage in stages)
            foreach(var action in stage)
                weights[action.AccountId]=weights.GetValueOrDefault(action.AccountId)+Weight(stage.Key)*common/stage.Count();
        var denominator=weights.Values.Aggregate(BigInteger.Zero,(a,b)=>a+b);
        var pieces=weights.Select(w=>(Account:w.Key,Amount:(long)(units*w.Value/denominator),Remainder:units*w.Value%denominator)).ToArray();
        var result=pieces.ToDictionary(p=>p.Account,p=>p.Amount);
        var left=units-result.Values.Sum();
        foreach(var piece in pieces.OrderByDescending(p=>p.Remainder).ThenBy(p=>p.Account).Take(checked((int)left)))result[piece.Account]++;
        return result;
    }
}
