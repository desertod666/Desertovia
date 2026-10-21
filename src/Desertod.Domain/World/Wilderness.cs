using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;

namespace Desertod.Domain.World;

public enum ObstacleKind
{
    /// <summary>Dense brambles; cut away for a little wood.</summary>
    Thicket,

    /// <summary>Boulders fallen into a mountain pass; broken up for stone.</summary>
    Rockfall,
}

/// <summary>One tile of a thicket or a rockfall. Nobody walks through it until citizens have worked it away.</summary>
public sealed class Obstacle
{
    public ObstacleKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>Work already done on the tile; it is cleared at <see cref="Wilderness.WorkNeeded"/>.</summary>
    public int Work { get; set; }

    /// <summary>When the tile was cleared; null while it still blocks the way.</summary>
    public DateTimeOffset? ClearedAt { get; set; }

    public TileArea Tile => new(X, Y, 1, 1);

    public TilePoint Center => new(X + 0.5, Y + 0.5);
}

/// <summary>The rows of the ravine in one column of tiles.</summary>
public sealed class RavineSpan
{
    public int X { get; set; }
    public int Top { get; set; }
    public int Height { get; set; }
}

/// <summary>A pass through a mountain ridge, 2×2 tiles from its top-left tile; a rockfall fills it.</summary>
public sealed class MountainPass
{
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>The ridge runs east to west, so the pass leads north to south; otherwise the other way round.</summary>
    public bool Horizontal { get; set; }
}

/// <summary>
/// What stands in the walkers' way out in the wild land, generated once per village. The web client draws the
/// mountain ridges around the cave, the lookout and the passes, and keeps walkers out of the ravine, thickets and
/// rockfalls; the server owns these because citizens clear them, dig in the cave and bridge the ravine.
/// </summary>
public sealed class Wilds
{
    public List<RavineSpan> Ravine { get; set; } = [];
    public List<MountainPass> Passes { get; set; } = [];
    public List<Obstacle> Obstacles { get; set; } = [];

    /// <summary>Tile of the cave mouth, set into the south face of a ridge.</summary>
    public int CaveX { get; set; }

    public int CaveY { get; set; }

    /// <summary>Tile at the top of the lookout peak; the trail up to it leaves from the tile below.</summary>
    public int LookoutX { get; set; }

    public int LookoutY { get; set; }

    public TilePoint CaveMouth => new(CaveX + 0.5, CaveY + 0.5);

    public TilePoint Lookout => new(LookoutX + 0.5, LookoutY + 0.5);
}

public static class Wilderness
{
    public const string Thicket = "thicket";
    public const string Rockfall = "rockfall";
    public const string Cave = "cave";

    /// <summary>How far, in tiles, a picked spot may be from the obstacle or the cave it is meant for.</summary>
    public const double SpotReach = 1.5;

    /// <summary>A cycle of clearing works on the obstacle tiles this close to the citizen's spot, nearest first.</summary>
    public const double PatchReach = 2.5;

    /// <summary>Chance per cycle, at a stat factor of 1, that digging in the cave turns up coal or iron ore.</summary>
    public const double CaveFindChance = 0.3;

    public static IReadOnlyList<LootEntry> CaveFinds { get; } =
    [
        new(ResourceKind.Coal, 1, 60),
        new(ResourceKind.IronOre, 1, 40),
    ];

    private const int RiverLeft = 30;
    private const int RiverRight = 33;

    /// <summary>Top row of the two-row road from the river to the east edge of the map; the web client keeps it bare.</summary>
    private const int RoadTop = 12;
    private const int CoreWidth = 44;
    private const int CoreHeight = 26;
    private const int RavineNorth = -22;
    private const int RavineSouth = -12;
    private const int PassCount = 3;
    private const int ThicketCount = 7;

    public static int WorkNeeded(ObstacleKind kind) => kind == ObstacleKind.Thicket ? 1 : 3;

    public static int WorkPerCycle(ObstacleKind kind) => kind == ObstacleKind.Thicket ? 3 : 2;

    public static ObstacleKind? ObstacleOf(TaskDefinition task) => task.Workplace switch
    {
        Thicket => ObstacleKind.Thicket,
        Rockfall => ObstacleKind.Rockfall,
        _ => null,
    };

    /// <summary>Tasks done out in the wild land at a place the server knows: an obstacle or the cave.</summary>
    public static bool IsWild(TaskDefinition task) => ObstacleOf(task) is not null || task.Workplace == Cave;

    public static bool Stands(Obstacle obstacle) => obstacle.ClearedAt is null;

    /// <summary>
    /// Where a wild task is worked: at the cave mouth, or at the obstacle the player picked, else at the nearest one the
    /// tribe has seen. Throws a rule error when there is nothing to work there.
    /// </summary>
    public static TilePoint WorkSpot(Village village, TaskDefinition task, TilePoint? picked)
    {
        var wilds = village.Wilds ?? throw new GameRuleException("wilds.unknown", "Nobody knows the wild land yet.");
        if (task.Workplace == Cave)
        {
            if (picked is { } spot && spot.DistanceTo(wilds.CaveMouth) > SpotReach)
            {
                throw new GameRuleException("spot.notForTask", $"\"{task.Name}\" is done at its own place.",
                    new Dictionary<string, object> { ["taskId"] = task.Id });
            }

            if (!village.IsExplored(new TileArea(wilds.CaveX, wilds.CaveY, 1, 1)))
            {
                throw new GameRuleException("wilds.caveUnknown", "Nobody has found the cave yet. Explore the mountains first.");
            }

            return wilds.CaveMouth;
        }

        var kind = ObstacleOf(task) ?? throw new ArgumentException("Not a wild task.", nameof(task));
        var standing = wilds.Obstacles.Where(o => o.Kind == kind && Stands(o)).ToList();
        if (picked is { } chosen)
        {
            var near = standing.Where(o => o.Center.DistanceTo(chosen) <= SpotReach).MinBy(o => o.Center.DistanceTo(chosen));
            return near?.Center ?? throw new GameRuleException("spot.nothingToClear", "Nothing is left to clear here.");
        }

        var camp = WorldMap.Places[WorldMap.Camp];
        var nearest = standing.Where(o => village.IsExplored(o.Tile)).MinBy(o => o.Center.DistanceTo(camp));
        return nearest?.Center ?? (kind == ObstacleKind.Thicket
            ? throw new GameRuleException("wilds.noThicket", "Nobody has found a thicket yet. Explore the land first.")
            : throw new GameRuleException("wilds.noRockfall", "Nobody has found a rockfall yet. Explore the mountains first."));
    }

    /// <summary>Works away obstacle tiles around the spot, nearest first, up to <paramref name="work"/>. Returns the work done.</summary>
    public static int Clear(Village village, ObstacleKind kind, int work, TilePoint spot, DateTimeOffset now)
    {
        var done = 0;
        var near = (village.Wilds?.Obstacles ?? [])
            .Where(o => o.Kind == kind && Stands(o) && o.Center.DistanceTo(spot) <= PatchReach)
            .OrderBy(o => o.Center.DistanceTo(spot))
            .ThenBy(o => o.Y)
            .ThenBy(o => o.X);
        foreach (var obstacle in near)
        {
            if (done >= work)
            {
                break;
            }

            var share = Math.Min(work - done, WorkNeeded(kind) - obstacle.Work);
            obstacle.Work += share;
            done += share;
            if (obstacle.Work >= WorkNeeded(kind))
            {
                obstacle.ClearedAt = now;
            }
        }

        return done;
    }

    /// <summary>
    /// A rope bridge crosses the ravine when the ravine runs through every column of its footprint, between the top row
    /// (the north rim) and the bottom row (the south rim, its yard).
    /// </summary>
    public static bool Spans(Village village, TileArea footprint)
    {
        if (village.Wilds is not { } wilds || !WorldMap.Land.Contains(footprint))
        {
            return false;
        }

        for (var x = footprint.X; x < footprint.X + footprint.Width; x++)
        {
            var span = wilds.Ravine.FirstOrDefault(s => s.X == x);
            if (span is null || span.Top <= footprint.Y || span.Top + span.Height >= footprint.Y + footprint.Height)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The ravine, the passes, the cave, the lookout and the thickets of a new village's wild land.</summary>
    public static Wilds Generate(Guid villageId)
    {
        var seed = Seed(villageId.ToString());
        var random = new Random(unchecked((int)seed));
        var wilds = new Wilds();
        var land = WorldMap.LegacyLand;

        // The ravine cuts the land north of the camp from the west edge of the map to the river.
        var top = random.Next(-18, -13);
        for (var x = land.X; x < land.X + land.Width; x++)
        {
            if (x > land.X && random.NextDouble() < 0.35)
            {
                top = Math.Clamp(top + (random.Next(2) == 0 ? -1 : 1), RavineNorth, RavineSouth);
            }

            var rows = Enumerable.Range(top, 2).Where(y => x < RiverWestBank(y, seed)).ToList();
            if (rows.Count == 0)
            {
                break;
            }

            wilds.Ravine.Add(new RavineSpan { X = x, Top = rows[0], Height = rows.Count });
        }

        var ravine = wilds.Ravine.SelectMany(s => Enumerable.Range(s.Top, s.Height).Select(y => new TilePoint(s.X + 0.5, y + 0.5))).ToList();
        var mountains = new List<TilePoint>();
        var thickets = new List<TilePoint>();

        bool Fits(int x, int y, int campClearance, int riverClearance, double ravineClearance)
        {
            var point = new TilePoint(x + 0.5, y + 0.5);
            return x >= land.X + 4 && y >= land.Y + 4 && x < land.X + land.Width - 4 && y < land.Y + land.Height - 4
                   && !(x >= -campClearance && y >= -campClearance && x < CoreWidth + campClearance && y < CoreHeight + campClearance)
                   && Math.Abs(x + 0.5 - RiverCenter(y, seed)) > riverClearance
                   && !(x > RiverRight && y >= RoadTop - 1 && y <= RoadTop + 2)
                   && ravine.All(r => r.DistanceTo(point) > ravineClearance);
        }

        TilePoint? Pick(int campClearance, int riverClearance, double ravineClearance, double mountainSpacing, double thicketSpacing)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var x = random.Next(land.X, land.X + land.Width);
                var y = random.Next(land.Y, land.Y + land.Height);
                var point = new TilePoint(x, y);
                if (Fits(x, y, campClearance, riverClearance, ravineClearance)
                    && mountains.All(m => m.DistanceTo(point) > mountainSpacing)
                    && thickets.All(t => t.DistanceTo(point) > thicketSpacing))
                {
                    return point;
                }
            }

            return null;
        }

        // Mountain features keep away from each other, so each gets a ridge of its own.
        var cave = Pick(10, 8, 6, 14, 0) ?? new TilePoint(-30, 20);
        mountains.Add(cave);
        wilds.CaveX = (int)cave.X;
        wilds.CaveY = (int)cave.Y;

        var lookout = Pick(10, 8, 6, 14, 0) ?? new TilePoint(-30, 40);
        mountains.Add(lookout);
        wilds.LookoutX = (int)lookout.X;
        wilds.LookoutY = (int)lookout.Y;

        for (var pass = 0; pass < PassCount; pass++)
        {
            if (Pick(10, 8, 6, 14, 0) is not { } point)
            {
                continue;
            }

            mountains.Add(point);
            var x = (int)point.X;
            var y = (int)point.Y;
            wilds.Passes.Add(new MountainPass { X = x, Y = y, Horizontal = random.Next(2) == 0 });
            foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                wilds.Obstacles.Add(new Obstacle { Kind = ObstacleKind.Rockfall, X = x + dx, Y = y + dy });
            }
        }

        // Thickets grow in patches out in the open land, clear of the mountains.
        for (var patch = 0; patch < ThicketCount; patch++)
        {
            if (Pick(3, 5, 4, 10, 9) is not { } center)
            {
                continue;
            }

            thickets.Add(center);
            var radius = 1.6 + random.NextDouble() * 1.2;
            for (var y = (int)Math.Floor(center.Y - radius); y <= center.Y + radius; y++)
            {
                for (var x = (int)Math.Floor(center.X - radius); x <= center.X + radius; x++)
                {
                    var reach = radius * (0.75 + 0.5 * random.NextDouble());
                    if (new TilePoint(x, y).DistanceTo(center) <= reach && Fits(x, y, 2, 3, 3))
                    {
                        wilds.Obstacles.Add(new Obstacle { Kind = ObstacleKind.Thicket, X = x, Y = y });
                    }
                }
            }
        }

        return wilds;
    }

    /// <summary>Centre column of the river in a row: straight through the camp area, meandering beyond it. Mirrors the web client.</summary>
    public static double RiverCenter(int y, uint seed)
    {
        var straight = (RiverLeft + RiverRight) / 2.0;
        var distance = y < 0 ? -y : y >= CoreHeight ? y - CoreHeight + 1 : 0;
        if (distance == 0)
        {
            return straight;
        }

        var phase = seed % 1000 / 160.0;
        var sway = Math.Sin(distance * 0.11 + phase) * 7 + Math.Sin(distance * 0.27 + phase * 2) * 2;
        return straight + sway * Math.Min(1, distance / 10.0);
    }

    /// <summary>The westernmost water tile of the river in a row. Mirrors the web client.</summary>
    public static int RiverWestBank(int y, uint seed) => (int)Math.Floor(RiverCenter(y, seed) - 1.5 + 0.5);

    /// <summary>FNV-1a over the UTF-16 code units of the text, as the web client seeds the land from the village id.</summary>
    public static uint Seed(string text)
    {
        var value = 2166136261;
        foreach (var unit in text)
        {
            value ^= unit;
            value = unchecked(value * 16777619);
        }

        return value;
    }
}
