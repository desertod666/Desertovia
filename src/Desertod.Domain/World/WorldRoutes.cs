using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;

namespace Desertod.Domain.World;

/// <summary>Stable destinations for the district atlas and future supplied expeditions.
/// Resource labels describe existing activities, not rewards for opening the atlas.</summary>
public sealed record WorldRoute(string Id,string District,IReadOnlyList<ResourceKind> Resources,
    string Danger,TilePoint? Destination,string? BlockedBy);

public static class WorldRoutes
{
    /// <summary>On-demand projection over the existing terrain and fog. Never generates a second world or rewrites coordinates.</summary>
    public static IReadOnlyList<WorldRoute> Discover(Village village,byte[] seen)
    {
        WorldRoute Route(string id,string district,TilePoint? target,string danger,params ResourceKind[] resources)
        {
            if(target is not {} point)return new(id,district,resources,danger,null,"unknown");
            var known=WorldMap.Land.Contains(point.ToTile())&&Exploration.IsExplored(seen,point.ToTile().X,point.ToTile().Y);
            // Exact coordinates of unrevealed generated landmarks remain hidden.
            return new(id,district,resources,danger,known?point:null,!known?"unexplored":!BankAccess.CanReach(village,point)?"bridge":null);
        }
        return [
            Route("settlement","home",WorldMap.Places[WorldMap.Camp],"low"),
            Route("deer-woods","western-woods",WorldMap.Places["deer-woods"],"moderate",ResourceKind.Food,ResourceKind.Leather),
            Route("ridge-cave","northern-ridge",village.Wilds?.CaveMouth,"high",ResourceKind.Coal,ResourceKind.IronOre),
            Route("lookout","northern-ridge",village.Wilds?.Lookout,"moderate"),
            Route("gold-vein","far-bank",WorldMap.Places[WorldMap.GoldVein],"high",ResourceKind.Gold)
        ];
    }
}
