using Desertod.Domain.Catalog;

namespace Desertod.Domain.Storage;

public enum StorageKind
{
    TownHall,
    Woodyard,
    StoneYard,
    Granary,
    Storehouse,
    Treasury,
    GreatGranary,
    Heap,
    WoodPile,
    StonePile,
    DryingRack,
}

/// <param name="Accepts">Goods this building holds. Empty means every good.</param>
public sealed record StorageDefinition(
    StorageKind Kind,
    string Name,
    IReadOnlyList<ResourceKind> Accepts,
    int Slots,
    int SlotSize)
{
    public bool AcceptsGood(ResourceKind kind) =>
        StorageCatalog.IsGood(kind) && (Accepts.Count == 0 || Accepts.Contains(kind));

    public int ResourceLimit => StorageLimits.PerResource(Kind);
    public int? TotalLimit => StorageLimits.Total(Kind);
    public bool IsGeneral => Accepts.Count == 0;
}

public static class StorageCatalog
{
    public static IReadOnlyList<StorageDefinition> Storages { get; } =
    [
        new(StorageKind.Heap, "Heap", [], 4, 10),
        new(StorageKind.WoodPile, "Wood pile", [ResourceKind.Wood, ResourceKind.Reeds], 2, 30),
        new(StorageKind.StonePile, "Stone pile", [ResourceKind.Stone, ResourceKind.Coal], 2, 30),
        new(StorageKind.DryingRack, "Drying rack", [ResourceKind.Food, ResourceKind.Porridge, ResourceKind.Stew, ResourceKind.Grain, ResourceKind.Leather, ResourceKind.Flax], 4, 30),
        new(StorageKind.TownHall, "Town Hall", [], 6, 20),
        new(StorageKind.Woodyard, "Woodyard", [ResourceKind.Wood, ResourceKind.Planks, ResourceKind.Reeds], 4, 50),
        new(StorageKind.StoneYard, "Stone yard", [ResourceKind.Stone, ResourceKind.IronOre, ResourceKind.Coal], 4, 40),
        new(StorageKind.Granary, "Granary", [ResourceKind.Food, ResourceKind.Porridge, ResourceKind.Stew, ResourceKind.Grain, ResourceKind.Leather, ResourceKind.Flax], 4, 60),
        new(StorageKind.Storehouse, "Storehouse",
            [
                ResourceKind.IronIngot, ResourceKind.Leather, ResourceKind.Feathers, ResourceKind.Flax, ResourceKind.Chain, ResourceKind.Mechanism,
                ResourceKind.Remedy, ResourceKind.VigorDrink, ResourceKind.Spear, ResourceKind.Bow, ResourceKind.Arrows, ResourceKind.Sword,
                ResourceKind.Shield, ResourceKind.LeatherArmor,
                ResourceKind.WoodenAxe, ResourceKind.StoneAxe, ResourceKind.IronAxe, ResourceKind.SteelAxe,
                ResourceKind.WoodenPickaxe, ResourceKind.StonePickaxe, ResourceKind.IronPickaxe, ResourceKind.SteelPickaxe,
            ], 8, 15),
        new(StorageKind.Treasury, "Treasury", [ResourceKind.Gold], 2, 50),
        new(StorageKind.GreatGranary, "Great Granary", [ResourceKind.Food, ResourceKind.Porridge, ResourceKind.Stew, ResourceKind.Grain, ResourceKind.Wood, ResourceKind.Stone], 6, 100),
    ];

    private static readonly Dictionary<StorageKind, StorageDefinition> ByKind = Storages.ToDictionary(s => s.Kind);

    public static StorageDefinition Get(StorageKind kind) => ByKind[kind];

    /// <summary>Knowledge is not a physical good: it is never stored in buildings and has no limit.</summary>
    public static bool IsGood(ResourceKind kind) => kind != ResourceKind.ResearchPoints;
}
