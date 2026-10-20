namespace Desertod.Domain.World;

public sealed class TripWaypoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Cost { get; set; }=1;
    [System.Text.Json.Serialization.JsonIgnore] public TilePoint Point=>new(X,Y);
}

/// <summary>A walk across the map. The land along it is seen only as the walker gets there.</summary>
public sealed class Trip
{
    public double FromX { get; set; }
    public double FromY { get; set; }
    public double ToX { get; set; }
    public double ToY { get; set; }
    public double SpeedMultiplier { get; set; } = 1;
    public DateTimeOffset SetOutAt { get; set; }
    private List<TripWaypoint>? waypoints;
    // Missing on historical JSON trips. Old journeys retain their original straight-line timing.
    public List<TripWaypoint> Waypoints {get=>waypoints??=[];set=>waypoints=value;}

    public TilePoint From => new(FromX, FromY);
    public TilePoint To => new(ToX, ToY);

    public DateTimeOffset ArrivesAt => SetOutAt + TimeSpan.FromSeconds(Duration);
    public double Duration
    {
        get
        {
            var from=From;var distance=0d;
            foreach(var step in Steps()){distance+=from.DistanceTo(step.Point)*step.Cost;from=step.Point;}
            return distance*Exploration.SecondsPerTile/Math.Max(.1,SpeedMultiplier);
        }
    }
    private IEnumerable<TripWaypoint> Steps()=>Waypoints.Count==0?[new(){X=ToX,Y=ToY}]:Waypoints;

    public static Trip Start(TilePoint from, TilePoint to, DateTimeOffset now) =>
        new() { FromX = from.X, FromY = from.Y, ToX = to.X, ToY = to.Y, SetOutAt = now };

    /// <summary>Where the walker is: on the way, or at the end once arrived.</summary>
    public TilePoint PositionAt(DateTimeOffset now)
    {
        return WalkedSegments(now).LastOrDefault((From,From)).Item2;
    }

    public IEnumerable<(TilePoint From,TilePoint To)> WalkedSegments(DateTimeOffset now)
    {
        if(now<SetOutAt)yield break;
        var remaining=(now-SetOutAt).TotalSeconds*Math.Max(.1,SpeedMultiplier)/Exploration.SecondsPerTile;
        var from=From;
        foreach(var step in Steps())
        {
            var length=from.DistanceTo(step.Point)*step.Cost;
            var share=length<=0?1:Math.Clamp(remaining/length,0,1);
            var to=new TilePoint(from.X+(step.X-from.X)*share,from.Y+(step.Y-from.Y)*share);
            yield return(from,to);
            if(remaining<length)yield break;
            remaining-=length;from=step.Point;
        }
    }
}

/// <summary>
/// Fog of war. The land the tribe has seen is one bitmap per village, shared by everyone in it: whatever one citizen
/// or the Tsar walks past, all players see. Tiles are numbered row by row from the top-left of
/// <see cref="WorldMap.Land"/>, eight to a byte, lowest bit first.
/// </summary>
public static class Exploration
{
    /// <summary>How far, in tiles, a walker sees around them.</summary>
    public const double SightRadius = 6;

    /// <summary>The camp land a new tribe knows: an oval around the campfire, reaching the river bank.</summary>
    public const double CampRadiusX = 19;

    public const double CampRadiusY = 17;

    /// <summary>Seen around buildings the tribe had before the fog of war, so none of them stands in the dark.</summary>
    public const double BuildingSightRadius = 4;

    /// <summary>The pace figures walk at on the map; the land ahead is seen no sooner than the map shows them there.</summary>
    public const double SecondsPerTile = 0.6;

    /// <summary>How far, in tiles, a walker sees from the top of the lookout peak.</summary>
    public const double LookoutSightRadius = 22;

    /// <summary>A walk that ends this close to the top of the lookout counts as climbing it.</summary>
    public const double LookoutReach = 1.5;

    public static int Size => (WorldMap.Land.Width * WorldMap.Land.Height + 7) / 8;

    public static int LegacySize => (WorldMap.LegacyLand.Width * WorldMap.LegacyLand.Height+7)/8;
    public static byte[] Upgrade(byte[] map)
    {
        if(map.Length==Size)return map;
        if(map.Length!=LegacySize)return Camp();
        var upgraded=new byte[Size];map.CopyTo(upgraded,0);return upgraded;
    }

    /// <summary>What a new tribe has seen: the camp land.</summary>
    public static byte[] Camp()
    {
        var map = new byte[Size];
        RevealOval(map, WorldMap.Places[WorldMap.Camp], CampRadiusX, CampRadiusY);
        return map;
    }

    public static bool IsExplored(byte[] map, int x, int y)
    {
        var land = WorldMap.Land;
        if (x < land.X || y < land.Y || x >= land.X + land.Width || y >= land.Y + land.Height || map.Length != Size)
        {
            return false;
        }

        var index = (y - land.Y) * land.Width + (x - land.X);
        return (map[index >> 3] & (1 << (index & 7))) != 0;
    }

    /// <summary>True when every tile of the area has been seen.</summary>
    public static bool IsExplored(byte[] map, TileArea area)
    {
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var x = area.X; x < area.X + area.Width; x++)
            {
                if (!IsExplored(map, x, y))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static void Reveal(byte[] map, TilePoint center, double radius) => RevealOval(map, center, radius, radius);

    /// <summary>
    /// Marks the land seen along the part of the trip walked by <paramref name="now"/>, and the wide view from the
    /// <paramref name="lookout"/> once the trip ends on top of it.
    /// </summary>
    public static void Reveal(byte[] map, Trip trip, DateTimeOffset now, TilePoint? lookout = null)
    {
        if (now < trip.SetOutAt)
        {
            return;
        }

        if (lookout is { } peak && now >= trip.ArrivesAt && trip.To.DistanceTo(peak) <= LookoutReach)
        {
            RevealOval(map, peak, LookoutSightRadius, LookoutSightRadius);
        }

        foreach(var (from,to) in trip.WalkedSegments(now))RevealSegment(map,from,to);
    }

    private static void RevealSegment(byte[] map,TilePoint from,TilePoint to)
    {
        var radius = SightRadius;
        var left = (int)Math.Floor(Math.Min(from.X, to.X) - radius);
        var right = (int)Math.Ceiling(Math.Max(from.X, to.X) + radius);
        var top = (int)Math.Floor(Math.Min(from.Y, to.Y) - radius);
        var bottom = (int)Math.Ceiling(Math.Max(from.Y, to.Y) + radius);
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (DistanceToSegment(new TilePoint(x + 0.5, y + 0.5), from, to) <= radius)
                {
                    Set(map, x, y);
                }
            }
        }
    }

    /// <summary>
    /// The village's seen land plus the parts of the trips walked by <paramref name="now"/>. Trips still count after
    /// they end, until the next trip of the same walker writes them into the village's map.
    /// </summary>
    public static byte[] Seen(byte[] explored, IEnumerable<Trip> trips, DateTimeOffset now, TilePoint? lookout = null)
    {
        var map = (byte[])Upgrade(explored).Clone();
        foreach (var trip in trips)
        {
            Reveal(map, trip, now, lookout);
        }

        return map;
    }

    /// <summary>
    /// A walker sets out to <paramref name="to"/>: the part of their last trip walked so far is written into the
    /// village's map, and the new trip starts where they are: on the way, or at <paramref name="resting"/>.
    /// </summary>
    public static Trip SetOut(byte[] explored, Trip? current, TilePoint resting, TilePoint to, DateTimeOffset now, TilePoint? lookout = null)
    {
        var from = resting;
        if (current is not null)
        {
            Reveal(explored, current, now, lookout);
            if (now < current.ArrivesAt)
            {
                from = current.PositionAt(now);
            }
        }

        return Trip.Start(from, to, now);
    }

    private static void RevealOval(byte[] map, TilePoint center, double radiusX, double radiusY)
    {
        for (var y = (int)Math.Floor(center.Y - radiusY); y <= (int)Math.Ceiling(center.Y + radiusY); y++)
        {
            for (var x = (int)Math.Floor(center.X - radiusX); x <= (int)Math.Ceiling(center.X + radiusX); x++)
            {
                var dx = (x + 0.5 - center.X) / radiusX;
                var dy = (y + 0.5 - center.Y) / radiusY;
                if (dx * dx + dy * dy <= 1)
                {
                    Set(map, x, y);
                }
            }
        }
    }

    private static void Set(byte[] map, int x, int y)
    {
        var land = WorldMap.Land;
        if (x < land.X || y < land.Y || x >= land.X + land.Width || y >= land.Y + land.Height)
        {
            return;
        }

        var index = (y - land.Y) * land.Width + (x - land.X);
        map[index >> 3] |= (byte)(1 << (index & 7));
    }

    private static double DistanceToSegment(TilePoint point, TilePoint from, TilePoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lengthSquared = dx * dx + dy * dy;
        var share = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - from.X) * dx + (point.Y - from.Y) * dy) / lengthSquared, 0, 1);
        return point.DistanceTo(new TilePoint(from.X + dx * share, from.Y + dy * share));
    }
}
