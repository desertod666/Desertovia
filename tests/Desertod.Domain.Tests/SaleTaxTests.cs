using Desertod.Domain;
using Desertod.Domain.Economy;

namespace Desertod.Domain.Tests;

public sealed class SaleTaxTests
{
    // Explicit synthetic fixtures, not selected owner rates or application defaults.
    private static TaxPolicy SyntheticScale()=>new(){Enabled=true,Brackets=[
        new(){FromProfit=0,RateBasisPoints=0},new(){FromProfit=200,RateBasisPoints=1000},
        new(){FromProfit=500,RateBasisPoints=2000},new(){FromProfit=1000,RateBasisPoints=3000}]};
    private static TaxPolicy SyntheticFlat(int bps)=>new(){Enabled=true,Brackets=[new(){FromProfit=0,RateBasisPoints=bps}]};

    [Theory]
    [InlineData(200,0)]
    [InlineData(500,30)]
    [InlineData(1000,130)]
    [InlineData(1400,250)]
    public void Marginal_brackets_tax_only_the_profit_in_each_bracket(long profit,long expected)
    {
        var result=SaleTaxRules.Calculate(SyntheticScale(),profit+100,100,true,0,0);
        Assert.Equal(profit,result.RealizedProfit);Assert.Equal(expected,result.TaxAmount);
        Assert.Equal(expected,SaleTaxRules.Liability(SyntheticScale(),profit));
    }

    [Fact]
    public void Unknown_history_is_not_zero_cost_and_old_or_disabled_offers_do_not_accrue_taxable_profit()
    {
        var unknown=SaleTaxRules.Calculate(SyntheticFlat(10000),100,0,false,0,123);
        Assert.Null(unknown.RealizedProfit);Assert.Equal("unknown_cost",unknown.ExemptReason);Assert.Equal(0,unknown.TaxAmount);Assert.Equal(123,unknown.RemainderAfter);
        var legacy=SaleTaxRules.Calculate(null,100,10,true,200,123);
        Assert.Equal("legacy_offer",legacy.ExemptReason);Assert.Equal(0,legacy.TaxableGain);
        var disabled=SaleTaxRules.Calculate(new(),100,10,true,200,123);
        Assert.Equal("policy_disabled",disabled.ExemptReason);Assert.Equal(0,disabled.TaxAmount);Assert.Equal(0,disabled.TaxableGain);
    }

    [Fact]
    public void Loss_is_not_a_debt_and_does_not_refund_tax_collected_on_other_sales()
    {
        var loss=SaleTaxRules.Calculate(SyntheticScale(),50,100,true,1000,9999);
        Assert.Equal(-50,loss.RealizedProfit);Assert.Equal(0,loss.TaxableGain);Assert.Equal(0,loss.TaxAmount);Assert.Equal(9999,loss.RemainderAfter);
    }

    [Fact]
    public void Splitting_tiny_sales_or_switching_accepted_policy_versions_cannot_discard_fractional_tax()
    {
        long paid=0,profit=0;var remainder=0;
        for(var i=0;i<10;i++)
        {
            var quote=SaleTaxRules.Calculate(SyntheticFlat(i%2==0?1000:2000),1,0,true,profit,remainder);
            profit+=quote.TaxableGain;paid+=quote.TaxAmount;remainder=quote.RemainderAfter;
        }
        Assert.Equal(1,paid);Assert.Equal(5000,remainder);Assert.Equal(10,profit);
        var whole=SaleTaxRules.Calculate(SyntheticFlat(1500),10,0,true,0,0);
        Assert.Equal(whole.TaxAmount,paid);Assert.Equal(whole.RemainderAfter,remainder);
    }

    [Fact]
    public void Very_large_profit_uses_wide_intermediates_and_never_taxes_the_material_cost()
    {
        var result=SaleTaxRules.Calculate(SyntheticFlat(10000),long.MaxValue,17,true,0,9999);
        Assert.Equal(long.MaxValue-17,result.TaxAmount);Assert.Equal(9999,result.RemainderAfter);
        Assert.Equal(long.MaxValue,SaleTaxRules.Liability(SyntheticFlat(10000),long.MaxValue));
        Assert.Throws<GameRuleException>(()=>SaleTaxRules.Calculate(SyntheticFlat(1000),2,0,true,long.MaxValue,0));
    }

    [Fact]
    public void Malformed_brackets_and_invalid_accumulated_state_are_rejected()
    {
        Assert.Throws<GameRuleException>(()=>SaleTaxRules.ValidatePolicy(new(){Enabled=true}));
        Assert.Throws<GameRuleException>(()=>SaleTaxRules.ValidatePolicy(new(){Enabled=true,Brackets=[new(){FromProfit=1,RateBasisPoints=1000}]}));
        Assert.Throws<GameRuleException>(()=>SaleTaxRules.ValidatePolicy(new(){Enabled=true,Brackets=[new(),new(){FromProfit=0,RateBasisPoints=1000}]}));
        Assert.Throws<GameRuleException>(()=>SaleTaxRules.ValidatePolicy(SyntheticFlat(10001)));
        Assert.Throws<GameRuleException>(()=>SaleTaxRules.Calculate(SyntheticFlat(1000),1,0,true,0,10000));
    }
}
