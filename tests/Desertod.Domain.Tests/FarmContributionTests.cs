using Desertod.Domain.Farming;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class FarmContributionTests
{
    private static readonly Guid A=Guid.Parse("00000000-0000-4000-8000-000000000001"),B=Guid.Parse("00000000-0000-4000-8000-000000000002"),C=Guid.Parse("00000000-0000-4000-8000-000000000003"),D=Guid.Parse("00000000-0000-4000-8000-000000000004");
    private static FarmContribution Action(Guid account,FarmStep step,int time=0)=>new(){AccountId=account,Name=account.ToString(),Step=step,StartedAt=Now.AddMinutes(time),CompletedAt=Now.AddMinutes(time+1)};

    [Fact]
    public void Owner_weights_distribute_one_fund_instead_of_paying_each_action()
    {
        var shares=FarmContributionRules.Allocate(100,[Action(A,FarmStep.Sow),Action(B,FarmStep.Water),Action(C,FarmStep.Fertilize),Action(D,FarmStep.Harvest)]);
        Assert.Equal(40,shares[A]);Assert.Equal(30,shares[B]);Assert.Equal(10,shares[C]);Assert.Equal(20,shares[D]);
    }

    [Fact]
    public void Rain_and_absent_fertilizer_normalize_only_performed_stages()
    {
        // A six-Food harvest has0.6Food for workers. Waiting/rain receive no invented contribution.
        var shares=FarmContributionRules.Allocate(600_000,[Action(A,FarmStep.Sow),Action(B,FarmStep.Harvest)]);
        Assert.Equal(400_000,shares[A]);Assert.Equal(200_000,shares[B]);Assert.Equal(600_000,shares.Values.Sum());
    }

    [Fact]
    public void Repeated_useful_waters_share_the_same_thirty_and_replayed_work_has_no_extra_weight()
    {
        var sow=Action(A,FarmStep.Sow);var water=Action(B,FarmStep.Water);
        var shares=FarmContributionRules.Allocate(100,[sow,water,water,Action(C,FarmStep.Water,30),Action(C,FarmStep.Water,60),Action(D,FarmStep.Fertilize),Action(D,FarmStep.Harvest)]);
        Assert.Equal(40,shares[A]);Assert.Equal(10,shares[B]);Assert.Equal(20,shares[C]);Assert.Equal(30,shares[D]);
    }

    [Fact]
    public void Tiny_and_large_funds_are_conserved_independently_of_input_order()
    {
        var actions=new[]{Action(A,FarmStep.Sow),Action(B,FarmStep.Water),Action(C,FarmStep.Water,30),Action(D,FarmStep.Harvest)};
        foreach(var units in new[]{0L,1,6,15,100,long.MaxValue})
        {
            var forward=FarmContributionRules.Allocate(units,actions);var reverse=FarmContributionRules.Allocate(units,actions.Reverse());
            Assert.Equal(units,forward.Values.Sum());Assert.All(forward,p=>Assert.Equal(p.Value,reverse[p.Key]));
            Assert.All(forward,p=>Assert.True(p.Value>=0));
        }
    }

    [Fact]
    public void No_crop_contributions_cannot_create_a_recipient_and_negative_funds_are_invalid()
    {
        Assert.Empty(FarmContributionRules.Allocate(1_000,[]));
        Assert.Throws<ArgumentOutOfRangeException>(()=>FarmContributionRules.Allocate(-1,[Action(A,FarmStep.Sow)]));
    }
}
