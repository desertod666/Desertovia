using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Api.Game;

/// <param name="ArrivesAt">Reaches the edge of the land then; the player's page moves to the dungeon once they do.</param>
/// <param name="Loot">Goods brought back; filled in when the hero is out.</param>
public sealed record DungeonHeroDto(
    Guid? CitizenId,
    string Name,
    HeroClass Class,
    DateTimeOffset? ArrivesAt,
    bool KnockedOut,
    EnemyKind? Killer,
    bool Settled,
    int Kills,
    int Experience,
    IReadOnlyList<ResourceAmount> Loot,
    IReadOnlyList<ResourceKind> Broken)
{
    public HeroClass? CombatClass { get; init; }
}

/// <summary>The village's dungeon run: open, under way, or ended a moment ago.</summary>
public sealed record DungeonDto(
    Guid Id,
    string DungeonId,
    DungeonPhase Phase,
    DungeonOutcome Outcome,
    DateTimeOffset OpenedAt,
    DateTimeOffset GatheringEndsAt,
    DateTimeOffset? MarchEndsAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int Slain,
    int Foes,
    IReadOnlyList<DungeonHeroDto> Heroes)
{
    public int CombatRulesVersion { get; init; }
}

/// <summary>A class the citizen may pick now, with what it would give them.</summary>
public sealed record HeroClassOfferDto(HeroClass Class, double Health, double Damage, double Range, ResourceKind? Weapon, ResourceKind? Armor);

public sealed record JoinDungeonRequest(HeroClass Class);

public sealed record OpenDungeonRequest(string DungeonId = DungeonCatalog.DeadLands);

/// <summary>Foe stats and the dungeon list for the client's help texts.</summary>
public sealed record DungeonInfoDto(string Id, string Name, int Width, int Height, int TimeLimitSeconds, int Foes);

/// <param name="Id">The citizen, or the Tsar's account.</param>
public sealed record DungeonRunHeroDto(Guid Id, Guid? CitizenId, string Name, HeroClass Class, Appearance? Appearance, double MaximumHealth)
{
    public HeroClass? CombatClass { get; init; }
}

/// <summary>Everything a viewer needs to draw a run: the map once, then <see cref="DungeonSnapshotDto"/> ten times a second.</summary>
/// <param name="Tiles">One digit per tile, row by row from the top-left: the <see cref="DungeonTile"/> value.</param>
/// <param name="MyHeroId">The viewer's own hero, if they fight in this run.</param>
/// <param name="Snapshot">Null while the run has not started or once it has ended.</param>
public sealed record DungeonRunDto(
    Guid RunId,
    string Slug,
    string DungeonId,
    int Width,
    int Height,
    string Tiles,
    TilePoint Entrance,
    int TimeLimitSeconds,
    IReadOnlyList<DungeonRunHeroDto> Heroes,
    Guid? MyHeroId,
    DungeonDto Run,
    DungeonSnapshotDto? Snapshot)
{
    public DungeonRunKind Kind { get; init; }
    public EnemyKind? EncounterEnemy { get; init; }
    public bool WorkSettlementPending { get; init; }
}

public sealed record DungeonHeroSnapshotDto(
    Guid Id,
    double X,
    double Y,
    double Health,
    HeroState State,
    int? TargetFoe,
    Guid? HealTarget,
    int? Arrows,
    int Kills,
    int Experience,
    IReadOnlyList<ResourceAmount> Loot,
    bool Retreating=false);

public sealed record DungeonFoeSnapshotDto(int Id, EnemyKind Kind, double X, double Y, double Health, double MaximumHealth, Guid? Target);

public sealed record DungeonSnapshotDto(
    Guid RunId,
    double Elapsed,
    DungeonOutcome Outcome,
    IReadOnlyList<DungeonHeroSnapshotDto> Heroes,
    IReadOnlyList<DungeonFoeSnapshotDto> Foes,
    IReadOnlyList<DungeonEvent> Events,
    int Floor,
    int FloorCount,
    int Width,
    int Height,
    string Tiles,
    TilePoint Entrance);

public static class DungeonMapper
{
    public static DungeonDto ToDto(this DungeonRun run) => new(
        run.Id,
        run.DungeonId,
        run.Phase,
        run.Outcome,
        run.OpenedAt,
        run.GatheringEndsAt,
        run.MarchEndsAt,
        run.StartedAt,
        run.EndedAt,
        run.Slain,
        run.Foes,
        run.Heroes
            .Select(h => new DungeonHeroDto(
                h.CitizenId,
                h.Name,
                h.Class,
                h.ArrivesAt,
                h.KnockedOut,
                h.Killer,
                h.Settled,
                h.Kills,
                h.Experience,
                h.Loot.Select(l => new ResourceAmount(l.Kind, l.Amount)).ToList(),
                h.Broken){CombatClass=h.CombatClass})
            .ToList()){CombatRulesVersion=run.CombatRulesVersion};

    public static string EncodeTiles(DungeonDefinition dungeon) => string.Concat(dungeon.Tiles.Select(t => (char)('0' + (int)t)));

    public static DungeonSnapshotDto Snapshot(Guid runId, DungeonSimulation simulation, IReadOnlyList<DungeonEvent> events) => new(
        runId,
        Math.Round(simulation.Elapsed, 2),
        simulation.Outcome,
        simulation.Heroes
            .Select(h => new DungeonHeroSnapshotDto(
                h.Id,
                Math.Round(h.X, 2),
                Math.Round(h.Y, 2),
                Math.Round(h.Health, 1),
                h.State,
                h.TargetFoe,
                h.HealTarget,
                h.Arrows,
                h.Kills,
                h.Experience,
                h.Loot.Select(l => new ResourceAmount(l.Key, l.Value)).ToList(),h.RetreatRequested))
            .ToList(),
        simulation.Foes
            .Select(f => new DungeonFoeSnapshotDto(f.Id, f.Kind, Math.Round(f.X, 2), Math.Round(f.Y, 2), Math.Round(Math.Max(0, f.Health), 1), f.Definition.Health, f.Target))
            .ToList(),
        events,
        simulation.Floor,
        simulation.FloorCount,
        simulation.Dungeon.Width,
        simulation.Dungeon.Height,
        EncodeTiles(simulation.Dungeon),
        simulation.Dungeon.Entrance);

    public static IReadOnlyList<DungeonInfoDto> Dungeons() =>
        DungeonCatalog.Dungeons
            .Select(d => new DungeonInfoDto(d.Id, d.Name, d.Width, d.Height, (int)d.TimeLimit.TotalSeconds, d.TotalFoes))
            .ToList();
}
