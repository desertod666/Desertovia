namespace Desertod.Domain.Catalog;

/// <summary>English resource names for server messages; clients translate by <see cref="ResourceKind"/>.</summary>
public static class ResourceNames
{
    public static string Of(ResourceKind kind) => kind switch
    {
        ResourceKind.IronOre => "iron ore",
        ResourceKind.IronIngot => "iron",
        ResourceKind.ResearchPoints => "knowledge",
        ResourceKind.LeatherArmor => "leather armor",
        ResourceKind.WoodenAxe => "wooden axe",
        ResourceKind.StoneAxe => "stone axe",
        ResourceKind.IronAxe => "iron axe",
        ResourceKind.SteelAxe => "steel axe",
        ResourceKind.WoodenPickaxe => "wooden pickaxe",
        ResourceKind.StonePickaxe => "stone pickaxe",
        ResourceKind.IronPickaxe => "iron pickaxe",
        ResourceKind.SteelPickaxe => "steel pickaxe",
        ResourceKind.Porridge => "grain porridge",
        ResourceKind.Stew => "hearty stew",
        ResourceKind.VigorDrink => "vigor drink",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
