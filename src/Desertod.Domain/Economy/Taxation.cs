namespace Desertod.Domain.Economy;

/// <summary>Immutable owner-published marginal rules. No rate is an application default.</summary>
public sealed class TaxPolicy
{
    public Guid VillageId { get; set; }
    public long Version { get; set; }
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string RequestFingerprint { get; set; } = "";
    public bool Enabled { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public List<TaxBracket> Brackets { get; set; } = [];
}

public sealed class TaxBracket
{
    public long FromProfit { get; set; }
    public int RateBasisPoints { get; set; }
}

/// <summary>Server-owned settlement evidence. Buyer-facing DTOs must not expose the seller's daily income or basis.</summary>
public sealed class MarketTaxQuote
{
    public Guid VillageId { get; set; }
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public Guid BuyerAccountId { get; set; }
    public Guid SellerAccountId { get; set; }
    public long? PolicyVersion { get; set; }
    public DateOnly Day { get; set; }
    public long ExpectedSellerTaxRevision { get; set; }
    public long Price { get; set; }
    public long CostBasis { get; set; }
    public bool CostBasisKnown { get; set; }
    public long? RealizedProfit { get; set; }
    public long TaxableGain { get; set; }
    public long TaxAmount { get; set; }
    public int RemainderAfter { get; set; }
    public string ExemptReason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public Guid? TradeRequestId { get; set; }
}

public sealed class MarketTaxEvent
{
    public Guid VillageId { get; set; }
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid SellerAccountId { get; set; }
    public long? PolicyVersion { get; set; }
    public DateOnly Day { get; set; }
    public long Price { get; set; }
    public long CostBasis { get; set; }
    public bool CostBasisKnown { get; set; }
    public long? RealizedProfit { get; set; }
    public long TaxableGain { get; set; }
    public long TaxAmount { get; set; }
    public long TaxableProfitBefore { get; set; }
    public long TaxableProfitAfter { get; set; }
    public int RemainderAfter { get; set; }
    public string ExemptReason { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed record SaleTaxCalculation(long? RealizedProfit,long TaxableGain,long TaxAmount,int RemainderAfter,string ExemptReason);

public static class SaleTaxRules
{
    public const int BasisPointScale=10_000;
    public static readonly TimeSpan QuoteLifetime=TimeSpan.FromMinutes(2);

    public static void ValidatePolicy(TaxPolicy policy)
    {
        if(!policy.Enabled)
        {
            if(policy.Brackets.Count!=0)throw new GameRuleException("tax.brackets","Disabled rules must have no tax brackets.");
            return;
        }
        if(policy.Brackets.Count is <1 or >16 || policy.Brackets[0].FromProfit!=0)
            throw new GameRuleException("tax.brackets","Choose one to16 marginal brackets starting at zero profit.");
        long previous=-1;
        foreach(var bracket in policy.Brackets)
        {
            if(bracket.FromProfit<=previous||bracket.RateBasisPoints is <0 or >BasisPointScale)
                throw new GameRuleException("tax.brackets","Profit thresholds must increase and rates must be between zero and100 percent.");
            previous=bracket.FromProfit;
        }
    }

    public static SaleTaxCalculation Calculate(TaxPolicy? policy,long price,long basis,bool basisKnown,long taxableProfitBefore,int remainderBefore)
    {
        if(price<1||basis<0||taxableProfitBefore<0||remainderBefore is <0 or >=BasisPointScale)
            throw new GameRuleException("tax.amount","Invalid sale or accumulated tax state.");
        if(!basisKnown)return new(null,0,0,remainderBefore,"unknown_cost");
        var profit=price-basis;
        if(policy is null)return new(profit,0,0,remainderBefore,"legacy_offer");
        ValidatePolicy(policy);
        if(!policy.Enabled)return new(profit,0,0,remainderBefore,"policy_disabled");
        if(profit<=0)return new(profit,0,0,remainderBefore,"no_profit");
        if(taxableProfitBefore>long.MaxValue-profit)
            throw new GameRuleException("tax.limit","The daily tax accounting limit has been reached.");
        var numerator=Integral(policy,taxableProfitBefore+profit)-Integral(policy,taxableProfitBefore)+remainderBefore;
        return new(profit,profit,(long)(numerator/BasisPointScale),(int)(numerator%BasisPointScale),"");
    }

    public static long Liability(TaxPolicy policy,long taxableProfit)
    {
        ValidatePolicy(policy);
        return !policy.Enabled?0:(long)(Integral(policy,Math.Max(0,taxableProfit))/BasisPointScale);
    }

    private static Int128 Integral(TaxPolicy policy,long profit)
    {
        Int128 numerator=0;
        for(var i=0;i<policy.Brackets.Count;i++)
        {
            var bracket=policy.Brackets[i];
            if(profit<=bracket.FromProfit)break;
            var end=i+1<policy.Brackets.Count?Math.Min(profit,policy.Brackets[i+1].FromProfit):profit;
            numerator+=(Int128)(end-bracket.FromProfit)*bracket.RateBasisPoints;
        }
        return numerator;
    }
}
