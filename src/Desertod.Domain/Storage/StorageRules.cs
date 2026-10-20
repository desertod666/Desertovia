using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;

namespace Desertod.Domain.Storage;

public static class StorageRules
{
    /// <summary>Most of one good a citizen may hold, whether taken out of storage or left over from a task.</summary>
    public const int TakeLimit = 10;
    public static int CarryLimit(Citizen citizen, DateTimeOffset now) => (int)Math.Floor(TakeLimit *
        (citizen.HasMedicalEffect("cast",now) ? .75 : citizen.HasMedicalEffect("cups",now) ? .8 : 1));

    /// <summary>Puts as much of the citizen's load into storage as fits. Returns what was unloaded.</summary>
    public static IReadOnlyList<ResourceAmount> Unload(Citizen citizen, Village village, string? targetPlotId = null)
    {
        // A destination that vanished during the walk must never redirect the load elsewhere.
        var destination = targetPlotId is null ? null : village.Storages.SingleOrDefault(s => s.PlotId == targetPlotId);
        if (targetPlotId is not null && (destination is null || destination.OwnerAccountId is not null)) return [];
        var unloaded = new List<ResourceAmount>();
        foreach (var good in citizen.Load.ToList())
        {
            var stored = destination is null
                ? village.Store(good.Kind, good.Amount, good.Sharpen, good.Durability)
                : destination.Put(good.Kind, good.Amount, good.Sharpen, good.Durability);
            if (stored > 0)
            {
                citizen.TakeFromLoad(good.Kind, stored, good.Sharpen, good.Durability);
                unloaded.Add(ToolCatalog.Amount(good.Kind, stored, good.Sharpen, good.Durability));
            }
        }

        return unloaded;
    }

    public static StorageBuilding UnloadTarget(Citizen citizen, Village village, string plotId)
    {
        var storage = village.Storages.SingleOrDefault(s => s.PlotId == plotId)
            ?? throw new GameRuleException("take.noStorage", "There is no storage building here.");
        // Private warehouse deposits use personal inventory and cost-basis accounting, not communal carry jobs.
        if (storage.OwnerAccountId is not null)
            throw new GameRuleException("building.private", "Use the personal warehouse deposit action.");
        if (!citizen.Load.Any(g => storage.FreeSpace(g.Kind) > 0))
            throw new GameRuleException("storage.targetFull", "This storage has no room for your load.");
        return storage;
    }

    /// <summary>
    /// An idle citizen takes up to <paramref name="amount"/> of a good out of one storage building into their hands,
    /// holding at most <see cref="TakeLimit"/>. Returns the amount taken.
    /// </summary>
    public static int Take(Citizen citizen, Village village, string plotId, ResourceKind kind, int amount, DateTimeOffset now, int? sharpen = null, int? durability = null)
    {
        if (amount < 1)
        {
            throw new GameRuleException("take.amount", "Take at least one.");
        }

        if (citizen.Status != CitizenStatus.Active)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        if (citizen.WorkState(now) == WorkState.Working)
        {
            throw new GameRuleException("take.working", "Finish the current task first.");
        }

        var building = village.Storages.SingleOrDefault(s => s.PlotId == plotId)
                       ?? throw new GameRuleException("take.noStorage", "There is no storage building here.");

        if (building.OwnerAccountId is {} owner && owner != citizen.AccountId)
            throw new GameRuleException("building.private", "This storage belongs to another player.");
        if (building.OwnerAccountId is not null)
        {
            if (ToolCatalog.Get(kind) is { } tool && durability is int wear && wear != tool.MaxDurability)
                throw new GameRuleException("take.nothing", $"There is no transferable {ResourceNames.Of(kind)} here.", Resource(kind));
            return Desertod.Domain.Economy.PrivateWarehouse.Withdraw(citizen, building, kind, amount);
        }
        var room = CarryLimit(citizen,now) - citizen.Carried(kind);
        if (room <= 0)
        {
            throw new GameRuleException("take.handsFull", $"You cannot carry more {ResourceNames.Of(kind)}.", Resource(kind));
        }

        var wanted = Math.Min(amount, room);
        var taken = 0;
        foreach (var good in building.Goods
            .Where(g => g.Kind == kind && (sharpen is null || g.Sharpen == sharpen) && (durability is null || g.Durability == durability))
            .OrderBy(g => g.Sharpen)
            .ThenBy(g => g.Durability)
            .ToList())
        {
            if (taken >= wanted)
            {
                break;
            }

            var slice = building.Take(kind, wanted - taken, good.Sharpen, good.Durability);
            if (slice > 0)
            {
                citizen.Carry(kind, slice, good.Sharpen, ToolCatalog.Get(kind) is null ? null : good.Durability);
                taken += slice;
            }
        }

        if (taken <= 0)
        {
            throw new GameRuleException("take.nothing", $"There is no {ResourceNames.Of(kind)} here.", Resource(kind));
        }

        return taken;
    }

    /// <summary>
    /// Throws away only the goods storage has no room for; the rest stays in hands to be carried back,
    /// so goods taken out of storage cannot be destroyed.
    /// </summary>
    public static void Drop(Citizen citizen, Village village)
    {
        if (citizen.Load.Count == 0)
        {
            throw new GameRuleException("hands.empty", "Your hands are empty.");
        }

        var unwanted = citizen.Load.Where(g => village.FreeSpace(g.Kind) <= 0).ToList();
        if (unwanted.Count == 0)
        {
            throw new GameRuleException("drop.roomInStorage", "Storage has room for what you carry. Carry it there instead.");
        }

        foreach (var good in unwanted)
        {
            citizen.TakeFromLoad(good.Kind, good.Amount, good.Sharpen);
        }
    }

    private static Dictionary<string, object> Resource(ResourceKind kind) => new() { ["resource"] = kind.ToString() };
}
