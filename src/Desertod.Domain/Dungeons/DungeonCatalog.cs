using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.World;

namespace Desertod.Domain.Dungeons;

/// <summary>What lies on a dungeon tile; the client draws each kind its own way.</summary>
public enum DungeonTile
{
    Ground,
    DeadGrass,
    Bones,
    Entrance,
    DeadTree,
    Grave,
    Fence,
    Mound,
    Rock,
}

/// <param name="Chance">Chance that one slain foe leaves this good for a hero.</param>
public sealed record DungeonLoot(ResourceKind Resource, int Minimum, int Maximum, double Chance);

/// <summary>How an enemy fights in a dungeon. Raid power in <see cref="EnemyCatalog"/> stays separate.</summary>
/// <param name="Speed">Tiles per second.</param>
/// <param name="AggroRange">A hero this close, in tiles, is chased until one of them falls.</param>
/// <param name="AttackRange">Reach of a blow or a bolt, in tiles.</param>
/// <param name="Experience">Experience every hero on the map gets when it is slain.</param>
public sealed record DungeonFoe(
    EnemyKind Kind,
    double Health,
    double Damage,
    double AttackSeconds,
    double Speed,
    double AggroRange,
    double AttackRange,
    int Experience,
    IReadOnlyList<DungeonLoot> Loot);

/// <param name="X">Tile of the foe at the start, its centre being X + 0.5.</param>
public sealed record DungeonSpawn(EnemyKind Kind, int X, int Y);

/// <summary>A dungeon map: a small land of its own, entered and left through <see cref="Entrance"/>.</summary>
public sealed class DungeonDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>Row by row from the top-left.</summary>
    public required IReadOnlyList<DungeonTile> Tiles { get; init; }

    public required TilePoint Entrance { get; init; }
    public required IReadOnlyList<DungeonSpawn> Spawns { get; init; }
    public required TimeSpan TimeLimit { get; init; }

    /// <summary>Later floors of one expedition. Empty when this map is the whole run.</summary>
    public IReadOnlyList<DungeonDefinition> Floors { get; init; } = [];

    /// <summary>Server-authored encounter enemies; older maps and checkpoints use the common catalog.</summary>
    public IReadOnlyDictionary<EnemyKind,DungeonFoe> FoeDefinitions { get; init; } = new Dictionary<EnemyKind,DungeonFoe>();

    public int TotalFoes => Floors.Count > 0 ? Floors.Sum(floor => floor.Spawns.Count) : Spawns.Count;

    public DungeonTile TileAt(int x, int y) => Tiles[y * Width + x];

    public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsWalkable(int x, int y) => Inside(x, y) && TileAt(x, y) <= DungeonTile.Entrance;
}

public static class DungeonCatalog
{
    public const string DeadLands = "dead-lands";

    /// <summary>One expedition: dead woods, then the graveyard, then the barrow king alone.</summary>
    private static readonly string[] WoodsRows =
    [
        "TTTTTTTTTTTTTTTTTTTT",
        "T..................T",
        "T..r....z..........T",
        "T.......r.....i....T",
        "T..T........z......T",
        "E...........r......T",
        "T.....i............T",
        "T..r........T......T",
        "T........z.........T",
        "T..................T",
        "T.....r............T",
        "TTTTTTTTTTTTTTTTTTTT",
    ];

    private static readonly string[] GravesRows =
    [
        "TTTTTTTTTTTTTTTTTTTT",
        "T.g.g..............T",
        "T....s.............T",
        "T..........h.......T",
        "T.g.g..............T",
        "T........ffff......T",
        "E..................T",
        "T...s..............T",
        "T.............h....T",
        "T......w...........T",
        "T..g.g......s...w..T",
        "TTTTTTTTTTTTTTTTTTTT",
    ];

    private static readonly string[] BarrowRows =
    [
        "TTTTTTTTTTTTTTTT",
        "TmmmmmmmmmmmmmmT",
        "Tm.............T",
        "Tm.............T",
        "Tm.....m.m.....T",
        "Tm.....mKm.....T",
        "E..............T",
        "Tm.....m.m.....T",
        "Tm.............T",
        "Tm.............T",
        "TmmmmmmmmmmmmmmT",
        "TTTTTTTTTTTTTTTT",
    ];

    public static IReadOnlyDictionary<EnemyKind, DungeonFoe> Foes { get; } = new Dictionary<EnemyKind, DungeonFoe>
    {
        [EnemyKind.Rat] = new(EnemyKind.Rat, 8, 2, 1.0, 2.6, 4, 0.9, 4,
            [new(ResourceKind.Food, 1, 2, 0.5), new(ResourceKind.Leather, 1, 1, 0.25)]),
        [EnemyKind.Zombie] = new(EnemyKind.Zombie, 22, 4, 1.6, 1.0, 5, 0.9, 8,
            [new(ResourceKind.Leather, 1, 1, 0.3), new(ResourceKind.IronOre, 1, 1, 0.2)]),
        [EnemyKind.Skeleton] = new(EnemyKind.Skeleton, 18, 5, 1.3, 1.6, 6, 0.9, 8,
            [new(ResourceKind.IronOre, 1, 2, 0.6), new(ResourceKind.Gold, 1, 1, 0.1)]),
        [EnemyKind.Imp] = new(EnemyKind.Imp, 12, 3, 0.8, 2.4, 6, 0.9, 6,
            [new(ResourceKind.Coal, 1, 2, 0.6), new(ResourceKind.Remedy, 1, 1, 0.1)]),
        [EnemyKind.Ghoul] = new(EnemyKind.Ghoul, 30, 6, 1.4, 1.8, 5, 1.0, 12,
            [new(ResourceKind.Leather, 1, 2, 0.5), new(ResourceKind.IronOre, 1, 1, 0.3)]),
        [EnemyKind.Wraith] = new(EnemyKind.Wraith, 16, 4, 1.8, 1.4, 7, 4, 10,
            [new(ResourceKind.Remedy, 1, 1, 0.2), new(ResourceKind.Gold, 1, 1, 0.15)]),
        [EnemyKind.BarrowKing] = new(EnemyKind.BarrowKing, 120, 10, 1.6, 1.2, 6, 1.2, 40,
            [
                new(ResourceKind.Gold, 2, 4, 1),
                new(ResourceKind.IronIngot, 2, 3, 1),
                new(ResourceKind.Sword, 1, 1, 0.4),
                new(ResourceKind.Shield, 1, 1, 0.3),
            ]),
    };

    public static IReadOnlyList<DungeonDefinition> Dungeons { get; } = [DeadLandsCourse()];

    /// <summary>Several maps played in order as one run. The first map is what viewers see before the fight.</summary>
    public static DungeonDefinition Expedition(string id, string name, TimeSpan timeLimit, params DungeonDefinition[] floors)
    {
        var first = floors[0];
        return new DungeonDefinition
        {
            Id = id,
            Name = name,
            Width = first.Width,
            Height = first.Height,
            Tiles = first.Tiles,
            Entrance = first.Entrance,
            Spawns = first.Spawns,
            TimeLimit = timeLimit,
            Floors = floors,
        };
    }

    private static DungeonDefinition DeadLandsCourse()
    {
        var limit = TimeSpan.FromMinutes(15);
        return Expedition(
            DeadLands,
            "Dead Lands",
            limit,
            Parse("dead-lands-woods", "Dead woods", WoodsRows, limit),
            Parse("dead-lands-graves", "Graveyard", GravesRows, limit),
            Parse("dead-lands-barrow", "Barrow", BarrowRows, limit));
    }

    public static DungeonDefinition? Find(string id) => Dungeons.FirstOrDefault(d => d.Id == id);

    public static DungeonDefinition Get(string id) =>
        Find(id) ?? throw new GameRuleException("dungeon.unknown", $"Unknown dungeon \"{id}\".", new Dictionary<string, object> { ["dungeonId"] = id });

    public static DungeonFoe Foe(EnemyKind kind) => Foes[kind];

    /// <summary>
    /// Reads a layout: <c>.</c> ground, <c>,</c> dead grass, <c>b</c> bones, <c>E</c> entrance; <c>T</c> dead tree,
    /// <c>g</c> grave, <c>f</c> fence, <c>m</c> mound and <c>R</c> rock block the way. A letter of a foe
    /// (<c>r</c> rat, <c>z</c> zombie, <c>s</c> skeleton, <c>i</c> imp, <c>h</c> ghoul, <c>w</c> wraith, <c>K</c> barrow king)
    /// stands on ground.
    /// </summary>
    public static DungeonDefinition Parse(string id, string name, IReadOnlyList<string> rows, TimeSpan timeLimit)
    {
        var width = rows[0].Length;
        if (rows.Any(row => row.Length != width))
        {
            throw new InvalidOperationException($"Every row of dungeon \"{id}\" must be {width} tiles wide.");
        }

        var tiles = new List<DungeonTile>(width * rows.Count);
        var spawns = new List<DungeonSpawn>();
        TilePoint? entrance = null;
        for (var y = 0; y < rows.Count; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = rows[y][x];
                EnemyKind? foe = cell switch
                {
                    'r' => EnemyKind.Rat,
                    'z' => EnemyKind.Zombie,
                    's' => EnemyKind.Skeleton,
                    'i' => EnemyKind.Imp,
                    'h' => EnemyKind.Ghoul,
                    'w' => EnemyKind.Wraith,
                    'K' => EnemyKind.BarrowKing,
                    _ => null,
                };
                if (foe is { } kind)
                {
                    spawns.Add(new DungeonSpawn(kind, x, y));
                }

                var tile = cell switch
                {
                    ',' => DungeonTile.DeadGrass,
                    'b' => DungeonTile.Bones,
                    'E' => DungeonTile.Entrance,
                    'T' => DungeonTile.DeadTree,
                    'g' => DungeonTile.Grave,
                    'f' => DungeonTile.Fence,
                    'm' => DungeonTile.Mound,
                    'R' => DungeonTile.Rock,
                    _ => DungeonTile.Ground,
                };
                if (tile == DungeonTile.Entrance)
                {
                    entrance = new TilePoint(x + 0.5, y + 0.5);
                }

                tiles.Add(tile);
            }
        }

        return new DungeonDefinition
        {
            Id = id,
            Name = name,
            Width = width,
            Height = rows.Count,
            Tiles = tiles,
            Entrance = entrance ?? throw new InvalidOperationException($"Dungeon \"{id}\" has no entrance."),
            Spawns = spawns,
            TimeLimit = timeLimit,
        };
    }
}
