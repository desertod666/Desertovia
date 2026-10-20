using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;

namespace Desertod.Domain.Economy;

/// <summary>Test-game money only. Account candles are never touched by this module.</summary>
public sealed class KingdomEconomy
{
    public Guid VillageId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public long TreasuryCoins { get; set; }
    public bool WorkContractsEnabled { get; set; }
    public DateOnly FundDay { get; set; }
    public int CandlesIssuedToday { get; set; }
}

public sealed class GameWallet
{
    public Guid VillageId { get; set; }
    public Guid AccountId { get; set; }
    public long Coins { get; set; }
    public long TestCandles { get; set; }
    public DateOnly IncomeDay { get; set; }
    public long EarnedToday { get; set; }
    public long ProfitToday { get; set; }
    public DateOnly ExchangeDay { get; set; }
    public int ExchangedToday { get; set; }
}

public sealed class EconomyReceipt
{
    public Guid VillageId { get; set; }
    public Guid RequestId { get; set; }
    public Guid AccountId { get; set; }
    public required string Fingerprint { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class LedgerEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? AccountId { get; set; }
    public required string Operation { get; set; }
    public required string Asset { get; set; }
    public required string Location { get; set; }
    public long Delta { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class MarketLot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid SellerAccountId { get; set; }
    public ResourceKind Resource { get; set; }
    public int Amount { get; set; }
    public long Price { get; set; }
    public long CostBasis { get; set; }
    public bool CostBasisKnown { get; set; }
    public required string Status { get; set; } = "open";
    public Guid? BuyerAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class PersonalGood
{
    public ResourceKind Kind { get; set; }
    public int Amount { get; set; }
    public long CostBasis { get; set; }
    /// <summary>False for legacy JSON without a recorded acquisition cost; zero is not proof of free acquisition.</summary>
    public bool CostBasisKnown { get; set; }
}

public readonly record struct InventoryCost(long Basis, bool Known);

public static class PersonalInventory
{
    public const int MaximumKinds = 24;
    public const int PerResource = 100;

    public static int Amount(Citizen citizen, ResourceKind kind) => citizen.PersonalGoods.SingleOrDefault(x => x.Kind == kind)?.Amount ?? 0;
    public static int Room(Citizen citizen, ResourceKind kind) =>
        Amount(citizen, kind) == 0 && citizen.PersonalGoods.Count >= MaximumKinds ? 0 : PerResource - Amount(citizen, kind);

    public static void Add(Citizen citizen, ResourceKind kind, int amount, long basis, bool basisKnown = true)
    {
        if (amount < 1 || basis < 0 || amount > Room(citizen, kind))
            throw new GameRuleException("inventory.full", "Not enough room in the personal inventory.");
        var good = citizen.PersonalGoods.SingleOrDefault(x => x.Kind == kind);
        if (good is null) { good = new PersonalGood { Kind = kind, CostBasisKnown = basisKnown }; citizen.PersonalGoods.Add(good); }
        else good.CostBasisKnown &= basisKnown;
        good.Amount = checked(good.Amount + amount);
        good.CostBasis = checked(good.CostBasis + basis);
    }

    public static long Remove(Citizen citizen, ResourceKind kind, int amount) => RemoveWithCost(citizen, kind, amount).Basis;

    public static InventoryCost RemoveWithCost(Citizen citizen, ResourceKind kind, int amount)
    {
        var good = citizen.PersonalGoods.SingleOrDefault(x => x.Kind == kind);
        if (amount < 1 || good is null || good.Amount < amount)
            throw new GameRuleException("inventory.missing", "You do not own enough of this material.");
        var basis = amount == good.Amount ? good.CostBasis : (long)((Int128)good.CostBasis * amount / good.Amount);
        good.Amount -= amount; good.CostBasis -= basis;
        if (good.Amount == 0) citizen.PersonalGoods.Remove(good);
        return new(basis, good.CostBasisKnown);
    }
}

public static class EconomyRules
{
    public const long MaximumBalance = 1_000_000_000_000;
    public static void Credit(GameWallet wallet, long amount)
    {
        if (amount < 0 || wallet.Coins > MaximumBalance - amount)
            throw new GameRuleException("economy.limit", "The wallet limit has been reached.");
        wallet.Coins += amount;
    }
    public static void Spend(GameWallet wallet, long amount)
    {
        if (amount < 1 || wallet.Coins < amount) throw new GameRuleException("economy.funds", "Not enough dezcoins.");
        wallet.Coins -= amount;
    }
    public static long TaxPreview(long income)
    {
        // Marginal test scale. Preview only: not a withdrawal from the wallet.
        income = Math.Max(0, income);
        return Math.Max(0, Math.Min(income, 500) - 200) / 10
            + Math.Max(0, Math.Min(income, 1000) - 500) / 5
            + Math.Max(0, income - 1000) * 3 / 10;
    }
    public static int StorePrice(ResourceKind kind) => kind switch
    {
        ResourceKind.Wood or ResourceKind.Reeds => 2,
        ResourceKind.Stone or ResourceKind.Food or ResourceKind.Grain => 3,
        ResourceKind.Planks or ResourceKind.Leather or ResourceKind.Coal => 8,
        ResourceKind.IronOre => 10,
        ResourceKind.IronIngot => 20,
        _ => 30,
    };
}

public static class PrivateWarehouse
{
    public static void Deposit(Citizen citizen, Desertod.Domain.Storage.StorageBuilding storage, ResourceKind kind, int amount)
    {
        if (storage.OwnerAccountId != citizen.AccountId) throw new GameRuleException("building.private", "This is not your private storage.");
        if (amount < 1 || storage.FreeSpace(kind) < amount) throw new GameRuleException("inventory.full", "Not enough storage space.");
        var costRemoved = PersonalInventory.RemoveWithCost(citizen, kind, amount);
        storage.Put(kind, amount);
        var cost = storage.PersonalCosts.SingleOrDefault(x => x.Kind == kind);
        if (cost is null) { cost = new PersonalGood { Kind = kind, CostBasisKnown = costRemoved.Known }; storage.PersonalCosts.Add(cost); }
        else cost.CostBasisKnown &= costRemoved.Known;
        cost.Amount += amount; cost.CostBasis = checked(cost.CostBasis + costRemoved.Basis);
    }

    public static int Withdraw(Citizen citizen, Desertod.Domain.Storage.StorageBuilding storage, ResourceKind kind, int requested)
    {
        if (storage.OwnerAccountId != citizen.AccountId) throw new GameRuleException("building.private", "This is not your private storage.");
        var durability = ToolCatalog.Get(kind)?.MaxDurability;
        var plain = storage.Goods.Where(g => g.Kind == kind && g.Sharpen == 0 &&
            (durability is null || g.Durability == durability)).Sum(g => g.Amount);
        var amount = Math.Min(requested, Math.Min(plain, PersonalInventory.Room(citizen, kind)));
        if (amount < 1) throw new GameRuleException("inventory.full", "Nothing can be taken into the personal inventory.");
        var cost = storage.PersonalCosts.SingleOrDefault(x => x.Kind == kind);
        if (cost is null || cost.Amount < amount) throw new GameRuleException("inventory.basis", "The private warehouse needs reconciliation.");
        var basis = amount == cost.Amount ? cost.CostBasis : (long)((Int128)cost.CostBasis * amount / cost.Amount);
        cost.Amount -= amount; cost.CostBasis -= basis;
        if (cost.Amount == 0) storage.PersonalCosts.Remove(cost);
        storage.Take(kind, amount, 0, durability); PersonalInventory.Add(citizen, kind, amount, basis, cost.CostBasisKnown);
        return amount;
    }
}
