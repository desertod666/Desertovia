using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;

namespace Desertod.Domain;

public enum ToolFamily
{
    Axe,
    Pickaxe,
}

/// <param name="SpeedMultiplier">Applied to the gathered amount. Bare hands stay at 1.</param>
/// <param name="MaxDurability">Uses before this tool breaks and leaves the citizen's hands.</param>
public sealed record ToolDefinition(ResourceKind Kind, ToolFamily Family, int Tier, double SpeedMultiplier, int MaxDurability);

public static class ToolCatalog
{
    public static IReadOnlyList<ToolDefinition> Tools { get; } =
    [
        new(ResourceKind.WoodenAxe, ToolFamily.Axe, 0, 1.0, 5),
        new(ResourceKind.StoneAxe, ToolFamily.Axe, 1, 1.25, 8),
        new(ResourceKind.IronAxe, ToolFamily.Axe, 2, 1.5, 12),
        new(ResourceKind.SteelAxe, ToolFamily.Axe, 3, 1.75, 20),
        new(ResourceKind.WoodenPickaxe, ToolFamily.Pickaxe, 0, 1.0, 5),
        new(ResourceKind.StonePickaxe, ToolFamily.Pickaxe, 1, 1.25, 8),
        new(ResourceKind.IronPickaxe, ToolFamily.Pickaxe, 2, 1.5, 12),
        new(ResourceKind.SteelPickaxe, ToolFamily.Pickaxe, 3, 1.75, 20),
    ];

    public static ToolDefinition? Get(ResourceKind kind) => Tools.FirstOrDefault(tool => tool.Kind == kind);

    /// <summary>Axes fell wood. Pickaxes break stone, coal, ore and gold.</summary>
    public static bool Fits(ToolDefinition tool, ResourceKind resource) => tool.Family switch
    {
        ToolFamily.Axe => resource == ResourceKind.Wood,
        ToolFamily.Pickaxe => resource is ResourceKind.Stone or ResourceKind.Coal or ResourceKind.IronOre or ResourceKind.Gold,
        _ => false,
    };

    /// <summary>
    /// Best usable tool in the citizen's hands for this resource: highest tier, and the most worn of that tier
    /// so a damaged tool is finished before a fresh one of the same tier.
    /// </summary>
    public static (ToolDefinition? definition, CarriedGood? carried) BestToolForResource(Citizen citizen, ResourceKind resource)
    {
        CarriedGood? bestGood = null;
        ToolDefinition? best = null;
        foreach (var good in citizen.Load)
        {
            var definition = Get(good.Kind);
            if (definition is null || good.Durability <= 0 || good.Amount <= 0 || !Fits(definition, resource))
            {
                continue;
            }

            if (best is null || definition.Tier > best.Tier || (definition.Tier == best.Tier && good.Durability < bestGood!.Durability))
            {
                best = definition;
                bestGood = good;
            }
        }

        return (best, bestGood);
    }

    /// <summary>Spends one use of one tool. Returns the kind when that tool breaks.</summary>
    public static ResourceKind? Wear(Citizen citizen, CarriedGood tool)
    {
        var definition = Get(tool.Kind);
        if (definition is null || tool.Amount <= 0 || tool.Durability <= 0)
        {
            return null;
        }

        var kind = tool.Kind;
        var sharpen = tool.Sharpen;
        var left = tool.Durability - 1;
        if (citizen.TakeFromLoad(kind, 1, sharpen, tool.Durability) == 0)
        {
            return null;
        }

        if (left > 0)
        {
            citizen.Carry(kind, 1, sharpen, left);
            return null;
        }

        return kind;
    }

    /// <summary>A carried or stored stack, with tool wear filled in. Other goods stay at 0.</summary>
    public static ResourceAmount Amount(ResourceKind kind, int amount, int sharpen, int durability)
    {
        var tool = Get(kind);
        return new ResourceAmount(kind, amount, sharpen, tool is null ? 0 : durability, tool?.MaxDurability ?? 0);
    }
}
