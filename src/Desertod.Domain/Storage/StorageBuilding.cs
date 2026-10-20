using Desertod.Domain.Catalog;
using Desertod.Domain;

namespace Desertod.Domain.Storage;

/// <summary>
/// A building that holds goods in slots. A slot holds one kind of good, so a building with free units
/// can still refuse a new kind when all its slots are taken.
/// </summary>
public sealed class StorageBuilding
{
    public StorageKind Kind { get; set; }
    public Guid? OwnerAccountId { get; set; }

    /// <summary>Unique within a village; identifies the building.</summary>
    public int Number { get; set; }
    public required string PlotId { get; set; }

    public DateTimeOffset BuiltAt { get; set; }

    /// <summary>Harm done by raiders, 0 to <see cref="Combat.CombatRules.MaximumDamage"/>; repaired by citizens.</summary>
    public int Damage { get; set; }
    public bool Decommissioning { get; set; }
    public DateTimeOffset? RuinedAt { get; set; }
    public List<StoredGood> Investment { get; set; } = [];

    public List<StoredGood> Goods { get; set; } = [];
    public List<Desertod.Domain.Economy.PersonalGood> PersonalCosts { get; set; } = [];

    public StorageDefinition Definition => StorageCatalog.Get(Kind);

    public static StorageBuilding Create(StorageKind kind, string plotId, DateTimeOffset now) =>
        new() { Kind = kind, PlotId = plotId, BuiltAt = now };

    public int Amount(ResourceKind kind) => Goods.Where(g => g.Kind == kind).Sum(g => g.Amount);

    public int SlotsUsed(int amount) => (amount + Definition.SlotSize - 1) / Definition.SlotSize;

    public int FreeSpace(ResourceKind kind)
    {
        if (!Definition.AcceptsGood(kind))
        {
            return 0;
        }

        if (Decommissioning || RuinedAt is not null || Damage>=100) return 0;
        var resourceRoom = Math.Max(0, StorageLimits.PerResource(Kind) - Amount(kind));
        var totalRoom = StorageLimits.Total(Kind) is { } total ? Math.Max(0,total-Goods.Sum(g=>g.Amount)) : int.MaxValue;
        return Math.Min(resourceRoom,totalRoom);
    }

    /// <summary>Stores as much as fits and returns the stored amount. A plain good is sharpen 0.</summary>
    /// <param name="durability">Remaining uses of a tool. Omitted means a fresh tool; other goods stay at 0.</param>
    public int Put(ResourceKind kind, int amount, int sharpen = 0, int? durability = null)
    {
        if (sharpen < 0 || kind is not (ResourceKind.Sword or ResourceKind.Spear or ResourceKind.Arrows))
        {
            sharpen = 0;
        }

        var tool = ToolCatalog.Get(kind);
        var uses = tool is null
            ? 0
            : durability is int given && given >= 1 && given <= tool.MaxDurability
                ? given
                : tool.MaxDurability;

        var stored = Math.Min(amount, FreeSpace(kind));
        if (stored <= 0)
        {
            return 0;
        }

        var good = Goods.SingleOrDefault(g => g.Kind == kind && g.Sharpen == sharpen && g.Durability == uses);
        if (good is null)
        {
            good = new StoredGood { Kind = kind, Sharpen = sharpen, Durability = uses };
            Goods.Add(good);
        }

        good.Amount += stored;
        return stored;
    }

    /// <summary>Takes up to <paramref name="amount"/>, dullest edge and most worn tool first, and returns the amount taken.</summary>
    public int Take(ResourceKind kind, int amount) => Take(kind, amount, null, null);

    public int Take(ResourceKind kind, int amount, int sharpen) => Take(kind, amount, sharpen, null);

    /// <param name="durability">One tool wear. Null takes every wear of the sharpen, most worn first.</param>
    public int Take(ResourceKind kind, int amount, int? sharpen, int? durability)
    {
        var taken = 0;
        foreach (var good in Goods
            .Where(g => g.Kind == kind && (sharpen is null || g.Sharpen == sharpen) && (durability is null || g.Durability == durability))
            .OrderBy(g => g.Sharpen)
            .ThenBy(g => g.Durability)
            .ToList())
        {
            if (taken >= amount)
            {
                break;
            }

            var slice = Math.Min(amount - taken, good.Amount);
            good.Amount -= slice;
            taken += slice;
            if (good.Amount <= 0)
            {
                Goods.Remove(good);
            }
        }

        return taken;
    }

    /// <summary>Fill of every occupied slot, 0 to 1, for drawing stacks on the map.</summary>
    public IEnumerable<(ResourceKind Kind, double Fill)> Slots()
    {
        var size = Definition.SlotSize;
        foreach (var good in Goods.OrderBy(g => g.Kind))
        {
            var remaining = good.Amount;
            while (remaining > 0)
            {
                var fill = Math.Min(size, remaining);
                yield return (good.Kind, (double)fill / size);
                remaining -= fill;
            }
        }
    }
}

public sealed class StoredGood
{
    public ResourceKind Kind { get; set; }
    public int Amount { get; set; }

    /// <summary>Edge ground at the smithy. 0 is an ordinary weapon; other goods stay at 0.</summary>
    public int Sharpen { get; set; }

    /// <summary>Remaining uses of a stored tool. Other goods stay at 0. A fresh tool is its maximum.</summary>
    public int Durability { get; set; }
}
