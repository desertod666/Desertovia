using System.Globalization;

namespace Desertod.Domain.World;

public readonly record struct TilePoint(double X, double Y)
{
    public double DistanceTo(TilePoint other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));

    /// <summary>The tile the point lies on.</summary>
    public TileArea ToTile() => new((int)Math.Floor(X), (int)Math.Floor(Y), 1, 1);
}

/// <summary>A rectangle of whole tiles.</summary>
public readonly record struct TileArea(int X, int Y, int Width, int Height)
{
    public bool Overlaps(TileArea other) =>
        X < other.X + other.Width && other.X < X + Width && Y < other.Y + other.Height && other.Y < Y + Height;

    public bool Contains(TileArea other) =>
        other.X >= X && other.Y >= Y && other.X + other.Width <= X + Width && other.Y + other.Height <= Y + Height;
}

/// <summary>
/// Tile coordinates of natural places and buildings. The web client draws the same map, so walking
/// times shown to players match where citizens walk.
/// </summary>
public static class WorldMap
{
    public const double SecondsPerTile = 2;

    /// <summary>A building takes 3×3 tiles; the row below it is its yard, where goods and materials lie.</summary>
    public const int PlotWidth = 3;
    public const int PlotHeight = 4;

    public const string Camp = "camp";
    public const string Pier = "pier";

    /// <summary>The river bank by the road, where the bridge will cross: depth and current are measured here.</summary>
    public const string Ford = "ford";

    /// <summary>A gold vein in the hill south of the monastery, on the far bank: reachable once the bridge stands.</summary>
    public const string GoldVein = "gold-vein";

    /// <summary>No fixed spot: the citizen wanders around and comes back to the camp.</summary>
    public const string Wilds = "wilds";

    public static IReadOnlyDictionary<string, TilePoint> Places { get; } = new Dictionary<string, TilePoint>
    {
        [Camp] = new(15.5, 13.5),
        ["forest"] = new(5, 8),
        ["stones"] = new(10.5, 2.5),
        ["berries"] = new(4.5, 18.5),
        ["reeds"] = new(28.5, 18),
        [Pier] = new(30.5, 16),
        [Ford] = new(29, 14.5),
        [GoldVein] = new(40, 17),
        ["hunting"] = new(9, 23),
        ["deer-woods"] = new(-3, 21.5),
        ["bird-grove"] = new(21, 23.5),
        ["gate"] = new(1, 13),
        ["fortifications"] = new(2.5,10.5),
    };

    /// <summary>The camp land west of the river, short of the reeds on its bank. A whole plot must fit inside.</summary>
    public static TileArea BuildArea { get; } = new(0, 0, 28, 26);

    /// <summary>The whole map the web client draws: the camp and the wild land around it.</summary>
    public static TileArea LegacyLand { get; } = new(-44, -32, 120, 90);

    // Extend south only: the original row stride and every existing fog bit keep their index.
    public static TileArea Land { get; } = new(-44, -32, 120, 150);

    /// <summary>Ground nobody builds on: the campfire and the sites of the village projects.</summary>
    public static IReadOnlyList<TileArea> ReservedAreas { get; } =
    [
        new(14, 12, 3, 3),
        new(26, 9, 4, 3),
        new(25, 22, 4, 3),
        new(18, 0, 3, 4),
    ];

    /// <summary>A plot id is the top-left tile of the building, written as "x,y".</summary>
    public static string PlotAt(int x, int y) => string.Create(CultureInfo.InvariantCulture, $"{x},{y}");

    /// <summary>Top-left tile of a plot, or null for an id that is not a plot.</summary>
    public static TilePoint? PlotPosition(string plotId)
    {
        var parts = plotId.Split(',');
        return parts.Length == 2
               && int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x)
               && int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y)
            ? new TilePoint(x, y)
            : null;
    }

    /// <summary>Tiles a building and its yard cover.</summary>
    public static TileArea? Footprint(string plotId) =>
        PlotPosition(plotId) is { } plot ? Footprint((int)plot.X, (int)plot.Y) : null;

    public static TileArea Footprint(int x, int y) => new(x, y, PlotWidth, PlotHeight);

    /// <summary>Workplaces that are not natural places stand for buildings; without the building they count as the camp.</summary>
    public static TilePoint Place(string workplace) => Places.GetValueOrDefault(workplace, Places[Camp]);

    /// <summary>Where a citizen stands to drop goods into a building or work on its site.</summary>
    public static TilePoint Yard(string plotId)
    {
        if(plotId=="fortifications")return Places[plotId];
        var plot = PlotPosition(plotId) ?? Places[Camp];
        return new TilePoint(plot.X + PlotWidth / 2.0, plot.Y + PlotHeight - 0.5);
    }

    public static TimeSpan Walk(TilePoint from, TilePoint to) =>
        TimeSpan.FromSeconds(Math.Round(from.DistanceTo(to) * SecondsPerTile));
}
