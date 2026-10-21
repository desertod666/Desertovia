using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Buildings;

public static class KingdomRules
{
    public static void FoundCastle(Village village,DateTimeOffset now)
    {
        if(village.Structures.Any(s=>s.Kind==BuildingKind.Castle))return;
        const string plot="14,9";
        if(village.AreaTaken(WorldMap.Footprint(plot)!.Value))throw new GameRuleException("castle.occupied","The central castle site is occupied.");
        Forest.Hold(village,WorldMap.Footprint(plot)!.Value,now);
        village.Structures.Add(new Structure{Kind=BuildingKind.Castle,PlotId=plot,Level=1,BuiltAt=now});
    }

    public static int Level(Village village) => village.Structures.Where(s => s.Kind == BuildingKind.Castle && !s.Decommissioning)
        .Select(s => s.Level).DefaultIfEmpty(0).Max();

    // Grow west, north and south. The river and its bridge remain independent gates.
    public static TileArea Area(Village village) => AreaForLevel(Level(village));
    public static TileArea AreaForLevel(int level) => level switch
    {
        >= 3 => new(-8, -6, 36, 38),
        2 => new(-4, -3, 32, 32),
        _ => WorldMap.BuildArea,
    };
    public static bool Inside(Village village, string plot) => WorldMap.Footprint(plot) is { } area && Area(village).Contains(area);

    public static bool ClearGround(Village village, TileArea area)
    {
        var seed = Wilderness.Seed(village.Id.ToString());
        var regional=area.Y+area.Height>WorldMap.LegacyLand.Y+WorldMap.LegacyLand.Height?RegionalTerrain.For(village.Id,village.Wilds):null;
        for (var x=area.X; x<area.X+area.Width; x++)
        for (var y=area.Y; y<area.Y+area.Height; y++)
        {
            if(regional is not null&&RegionalTerrain.Inside(x,y)&&AuthoredTerrain.Blocked(regional[RegionalTerrain.Index(x,y)]))return false;
            if (Math.Abs(x+.5-Wilderness.RiverCenter(y,seed)) < 2) return false;
            if (village.Wilds?.Ravine.Any(r=>r.X==x && y>=r.Top && y<r.Top+r.Height)==true) return false;
            if (village.Wilds?.Obstacles.Any(o=>o.X==x && o.Y==y && Wilderness.Stands(o))==true) return false;
        }
        return true;
    }
}
