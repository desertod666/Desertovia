using System.Collections.Concurrent;

namespace Desertod.Domain.World;

/// <summary>Version2 extends the old authored terrain without consuming any of its random stream.</summary>
public static class RegionalTerrain
{
    public const int Version=2;
    public static TilePoint MarshTrail { get; }=new(14.5,92.5);
    public static TilePoint QuarryTrail { get; }=new(54.5,104.5);
    private static readonly ConcurrentDictionary<uint,byte[]> Extensions=new();
    public static int Index(int x,int y)=>(y-WorldMap.Land.Y)*WorldMap.Land.Width+x-WorldMap.Land.X;
    public static bool Inside(int x,int y)=>WorldMap.Land.Contains(new(x,y,1,1));
    public static bool NewLand(TilePoint p)=>Inside((int)Math.Floor(p.X),(int)Math.Floor(p.Y))&&!WorldMap.LegacyLand.Contains(p.ToTile());

    public static byte[] For(Guid village,Wilds? wilds)
    {
        var seed=Wilderness.Seed(village.ToString());
        if(Extensions.Count>256)Extensions.Clear();
        var extra=Extensions.GetOrAdd(seed,s=>GenerateExtension(s));
        var result=new byte[WorldMap.Land.Width*WorldMap.Land.Height];
        AuthoredTerrain.For(village,wilds).CopyTo(result,0);
        extra.CopyTo(result,WorldMap.LegacyLand.Width*WorldMap.LegacyLand.Height);
        return result;
    }

    /// <summary>Mirrored by regionalTerrain.ts. Stable coordinate noise; no regeneration of old ponds or ridges.</summary>
    public static byte Kind(uint seed,int x,int y)
    {
        var river=Wilderness.RiverWestBank(y,seed);
        if(x>=river&&x<river+4)return AuthoredTerrain.Water;
        var west=x<river;
        if((west&&x is 14 or 15)||(x is 50 or 51)||
           (y is 92 or 93&&x>=-26&&x<river-1)||(y is 104 or 105&&x>=45&&x<=66))return AuthoredTerrain.Trail;
        if(Math.Abs(x-54)<=3&&Math.Abs(y-104)<=3)return 3; // Worked stone outcrop and its approach.
        foreach(var p in new[]{(-18,82),(-7,106),(63,83)})
        {
            var radius=Math.Pow((x-p.Item1)/4d,2)+Math.Pow((y-p.Item2)/3d,2);
            if(radius<=1)return AuthoredTerrain.Water;
            if(radius<=2.8)return AuthoredTerrain.Marsh;
        }
        if(!west&&y is >=98 and <=100&&x is >=60 and <=74&&x is not (65 or 66))return AuthoredTerrain.Mountain;
        if(west&&x is >=3 and <=12&&y is >=89 and <=97)return 5;
        var value=unchecked(seed^(uint)(x*0x27d4eb2d)^(uint)(y*0x165667b1));
        value=unchecked((value^(value>>16))*0x85ebca6b);value=unchecked((value^(value>>13))*0xc2b2ae35);value^=value>>16;
        return (value%20) switch {<6=>1,6=>4,7=>3,_=>0};
    }
    private static byte[] GenerateExtension(uint seed)
    {
        var result=new byte[WorldMap.Land.Width*(WorldMap.Land.Height-WorldMap.LegacyLand.Height)];
        for(var y=WorldMap.LegacyLand.Y+WorldMap.LegacyLand.Height;y<WorldMap.Land.Y+WorldMap.Land.Height;y++)
        for(var x=WorldMap.Land.X;x<WorldMap.Land.X+WorldMap.Land.Width;x++)
            result[(y-58)*WorldMap.Land.Width+x-WorldMap.Land.X]=Kind(seed,x,y);
        return result;
    }
}
