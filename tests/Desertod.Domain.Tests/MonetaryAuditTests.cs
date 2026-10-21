using Desertod.Domain;
using Desertod.Domain.Economy;

namespace Desertod.Domain.Tests;

public sealed class MonetaryAuditTests
{
    private static readonly Guid Alice = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly MonetaryPosition A = new("wallet", Alice), B = new("wallet", Bob), Treasury = new("treasury", null);
    private static LedgerEntry Leg(Guid op, string action, long amount, Guid? account = null, string location = "wallet") =>
        new() { VillageId = Guid.Empty, OperationId = op, Operation = action, Asset = "DZK", Location = location, AccountId = account, Delta = amount };

    [Fact]
    public void Taxed_sale_is_one_transfer_across_buyer_seller_and_treasury()
    {
        var op = Guid.NewGuid();
        var result = Assert.Single(MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long> { [A] = 300 },
            new Dictionary<MonetaryPosition, long> { [B] = 290, [Treasury] = 10 },
            [Leg(op, "trade", -300, Alice), Leg(op, "trade", 290, Bob), Leg(op, "sale-tax", 10, Bob, "treasury")]));
        Assert.Equal("transfer", result.Kind); Assert.Equal(300, result.Transferred); Assert.Equal(0, result.Issued);
    }

    [Fact]
    public void Equal_total_does_not_hide_an_unrecorded_transfer_between_wallets()
    {
        Assert.Throws<GameRuleException>(() => MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long> { [A] = 100, [B] = 100 },
            new Dictionary<MonetaryPosition, long> { [A] = 99, [B] = 101 }, []));
    }

    [Theory]
    [InlineData("wage", 10, "legacy-wage-issuance")]
    [InlineData("energy-drink", -300, "withdrawal")]
    [InlineData("exchange-burn", -2000000, "withdrawal")]
    public void Existing_issuance_and_withdrawals_are_explicit(string action, long delta, string kind)
    {
        var result = Assert.Single(MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long> { [A] = 2000000 },
            new Dictionary<MonetaryPosition, long> { [A] = 2000000 + delta }, [Leg(Guid.NewGuid(), action, delta, Alice)]));
        Assert.Equal(kind, result.Kind); Assert.Equal(delta, result.Issued - result.Withdrawn);
    }

    [Theory]
    [InlineData("trade", 10)]
    [InlineData("unknown", 10)]
    [InlineData("wage", 1001)]
    [InlineData("wage", -10)]
    [InlineData("energy-drink", -299)]
    [InlineData("exchange-burn", -2000001)]
    public void Balanced_bookkeeping_alone_cannot_authorize_minting_or_an_arbitrary_debit(string action, long delta)
    {
        Assert.Throws<GameRuleException>(() => MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long> { [A] = 3000000 },
            new Dictionary<MonetaryPosition, long> { [A] = 3000000 + delta }, [Leg(Guid.NewGuid(), action, delta, Alice)]));
    }

    [Fact]
    public void Reserve_refund_is_a_transfer_and_large_totals_do_not_overflow_long()
    {
        var reserve = MonetaryPosition.From("work-reserve:" + Guid.NewGuid(), Alice); var op = Guid.NewGuid();
        var result = Assert.Single(MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long> { [reserve] = long.MaxValue, [A] = long.MaxValue },
            new Dictionary<MonetaryPosition, long> { [Treasury] = long.MaxValue, [A] = long.MaxValue },
            [Leg(op, "work-refund", -long.MaxValue, null, reserve.Location), Leg(op, "work-refund", long.MaxValue, Alice, "treasury")]));
        Assert.Equal(long.MaxValue, result.Transferred);
    }

    [Fact]
    public void Negative_holding_unknown_location_and_other_currencies_are_rejected()
    {
        Assert.Throws<GameRuleException>(() => MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long> { [A] = -1 },
            new Dictionary<MonetaryPosition, long> { [A] = -1 }, []));
        Assert.Throws<GameRuleException>(() => MonetaryPosition.From("work-reserve:wrong", null));
        Assert.Throws<GameRuleException>(() => MonetaryPosition.From("wallet", null));
        var candle = Leg(Guid.NewGuid(), "exchange", 1, Alice); candle.Asset = "TEST_CANDLE";
        Assert.Throws<GameRuleException>(() => MonetaryAudit.Reconcile(new Dictionary<MonetaryPosition, long>(), new Dictionary<MonetaryPosition, long>(), [candle]));
    }
}
