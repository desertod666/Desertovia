using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;

namespace Desertod.Domain.Economy;

public enum SupplyOrderStatus { Open, Cancelled, Expired, Completed }

/// <summary>A funded public purchase request. Goods remain personal until an actual delivery commits.</summary>
public sealed class SupplyOrder
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid IssuerAccountId { get; set; }
    public ResourceKind Resource { get; set; }
    public int PolicyVersion { get; set; } = 1;
    public int RequestedAmount { get; set; }
    public int RemainingAmount { get; set; }
    public int DeliveredAmount { get; set; }
    public long UnitPrice { get; set; }
    public long ReservedCoins { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public SupplyOrderStatus Status { get; set; }
}

public sealed class TradeReserve
{
    public ResourceKind Resource { get; set; }
    public int MinimumAmount { get; set; }
}

/// <summary>Immutable cost and payment evidence for one committed delivery; no additional goods are held here.</summary>
public sealed class SupplyDelivery
{
    public Guid Id { get; set; }
    public Guid VillageId { get; set; }
    public Guid SupplyOrderId { get; set; }
    public Guid SupplierAccountId { get; set; }
    public ResourceKind Resource { get; set; }
    public int Amount { get; set; }
    public long PaidCoins { get; set; }
    public long CostBasis { get; set; }
    public bool CostBasisKnown { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class SupplyOrderRules
{
    public static long Budget(int amount, long unitPrice)
    {
        if (amount is < 1 or > 10_000 || unitPrice is < 1 or > 1_000_000)
            throw new GameRuleException("supplyOrder.terms", "Choose 1–10000 units and a positive unit price up to 1000000.");
        return checked(amount * unitPrice);
    }

    /// <summary>Caller atomically moves the personal goods to public storage and pays from this fund.</summary>
    public static long Deliver(SupplyOrder order, int amount, DateTimeOffset now)
    {
        if (order.Status != SupplyOrderStatus.Open || now >= order.ExpiresAt)
            throw new GameRuleException("supplyOrder.closed", "This supply order is closed.");
        if (amount < 1 || amount > order.RemainingAmount)
            throw new GameRuleException("supplyOrder.amount", "Choose no more than the remaining quantity.");
        var pay = checked(amount * order.UnitPrice);
        if (order.ReservedCoins < pay)
            throw new InvalidOperationException("The supply order has lost its reserved fund.");
        order.RemainingAmount -= amount;
        order.DeliveredAmount += amount;
        order.ReservedCoins -= pay;
        if (order.RemainingAmount == 0) order.Status = SupplyOrderStatus.Completed;
        return pay;
    }

    public static long Close(SupplyOrder order, SupplyOrderStatus status)
    {
        if (status is not (SupplyOrderStatus.Cancelled or SupplyOrderStatus.Expired))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (order.Status != SupplyOrderStatus.Open) return 0;
        var refund = order.ReservedCoins;
        order.ReservedCoins = 0;
        order.Status = status;
        return refund;
    }
}

public static class TradeReserveRules
{
    public static void Set(KingdomEconomy economy, ResourceKind resource, int minimum)
    {
        if (!StorageCatalog.IsGood(resource) || minimum is < 0 or > 100_000)
            throw new GameRuleException("market.reserveAmount", "Choose a stored resource and a reserve from 0 to 100000.");
        var reserve = economy.TradeReserves.SingleOrDefault(r => r.Resource == resource);
        if (minimum == 0)
        {
            if (reserve is not null) economy.TradeReserves.Remove(reserve);
            return;
        }
        if (reserve is null) economy.TradeReserves.Add(new TradeReserve { Resource = resource, MinimumAmount = minimum });
        else reserve.MinimumAmount = minimum;
    }

    public static int PlainPublicStock(Village village, ResourceKind kind)
    {
        var durability = ToolCatalog.Get(kind)?.MaxDurability;
        return village.Storages.Where(s => s.OwnerAccountId is null && s.RuinedAt is null && s.Damage < 100)
            .SelectMany(s => s.Goods).Where(g => g.Kind == kind && g.Sharpen == 0
                && (durability is null || g.Durability == durability)).Sum(g => g.Amount);
    }

    public static int ForSale(Village village, KingdomEconomy economy, ResourceKind kind) =>
        Math.Max(0, PlainPublicStock(village, kind) - (economy.TradeReserves.SingleOrDefault(r => r.Resource == kind)?.MinimumAmount ?? 0));
}
