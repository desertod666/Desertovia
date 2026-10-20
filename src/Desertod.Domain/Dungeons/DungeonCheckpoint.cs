using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.World;

namespace Desertod.Domain.Dungeons;

// Persistence state uses full precision. The rounded transport snapshot cannot resume a fight.
public sealed record DungeonPathState(TilePoint? Goal, IReadOnlyList<TilePoint> Waypoints);
public sealed record DungeonHeroState(
    DungeonFighter Fighter, double X, double Y, double Health, HeroState State,
    int? TargetFoe, Guid? HealTarget, TilePoint? Destination, int? Arrows,
    int Kills, int Experience, IReadOnlyDictionary<ResourceKind, int> Loot,
    IReadOnlyList<int> FoesHit, bool Healed, EnemyKind? Killer, bool ExternallyWithdrawn,
    double Cooldown, DungeonPathState Path);
public sealed record DungeonFoeState(
    int Id, DungeonFoe Definition, double X, double Y, double Health,
    Guid? Target, double Cooldown, DungeonPathState Path);
public sealed record DungeonCheckpoint(
    int Version, DungeonDefinition Definition, int Floor, int SlainBefore,
    double Elapsed, DungeonOutcome Outcome, bool TimedOut,
    IReadOnlyList<DungeonHeroState> Heroes, IReadOnlyList<DungeonFoeState> Foes);

public sealed partial class DungeonSimulation
{
    public DungeonCheckpoint Capture() => new(1, definition, floor, slainBefore, Elapsed, Outcome, TimedOut,
        heroes.Select(h => new DungeonHeroState(h.Fighter, h.X, h.Y, h.Health, h.State,
            h.TargetFoe, h.HealTarget, h.Destination, h.Arrows, h.Kills, h.Experience,
            new Dictionary<ResourceKind, int>(h.Loot), h.FoesHit.Order().ToArray(), h.Healed,
            h.Killer, h.ExternallyWithdrawn, h.Cooldown, CapturePath(h.Path))).ToArray(),
        foes.Select(f => new DungeonFoeState(f.Id, f.Definition, f.X, f.Y, f.Health,
            f.Target, f.Cooldown, CapturePath(f.Path))).ToArray());

    public static DungeonSimulation Restore(DungeonCheckpoint state)
    {
        if (state is null || state.Definition is null || state.Heroes is null || state.Foes is null
            || state.Heroes.Any(h => h is null || h.Fighter is null || h.Path is null || h.Loot is null || h.FoesHit is null)
            || state.Foes.Any(f => f is null || f.Definition is null || f.Path is null))
            throw new InvalidOperationException("Incomplete dungeon checkpoint.");
        if (state.Version != 1) throw new InvalidOperationException("Unsupported dungeon checkpoint version.");
        var simulation = new DungeonSimulation(state.Definition, state.Heroes.Select(h => h.Fighter));
        if (state.Floor < 0 || state.Floor >= simulation.floors.Count
            || state.Foes.Select((f, index) => f.Id != index).Any(invalid => invalid)
            || state.Heroes.Select(h => h.Fighter.Id).Distinct().Count() != state.Heroes.Count)
            throw new InvalidOperationException("Invalid dungeon checkpoint identities or floor.");
        if (state.Foes.Count != simulation.floors[state.Floor].Spawns.Count)
            throw new InvalidOperationException("Dungeon checkpoint does not contain every foe.");
        simulation.floor = state.Floor;
        simulation.Dungeon = simulation.floors[state.Floor];
        simulation.slainBefore = state.SlainBefore;
        simulation.Elapsed = state.Elapsed;
        simulation.Outcome = state.Outcome;
        simulation.TimedOut = state.TimedOut;
        simulation.heroes.Clear();
        foreach (var h in state.Heroes)
        {
            if (h.TargetFoe is { } target && (target < 0 || target >= state.Foes.Count))
                throw new InvalidOperationException("Invalid dungeon checkpoint target.");
            var hero = new SimulatedHero
            {
                Fighter = h.Fighter, X = h.X, Y = h.Y, Health = h.Health, State = h.State,
                TargetFoe = h.TargetFoe, HealTarget = h.HealTarget, Destination = h.Destination,
                Arrows = h.Arrows, Kills = h.Kills, Experience = h.Experience, Healed = h.Healed,
                Killer = h.Killer, ExternallyWithdrawn = h.ExternallyWithdrawn, Cooldown = h.Cooldown,
            };
            foreach (var pair in h.Loot) hero.Loot.Add(pair.Key, pair.Value);
            hero.FoesHit.UnionWith(h.FoesHit);
            RestorePath(hero.Path, h.Path);
            simulation.heroes.Add(hero);
        }
        simulation.foes.Clear();
        foreach (var f in state.Foes)
        {
            var foe = new SimulatedFoe
            {
                Id = f.Id, Definition = f.Definition, X = f.X, Y = f.Y,
                Health = f.Health, Target = f.Target, Cooldown = f.Cooldown,
            };
            RestorePath(foe.Path, f.Path);
            simulation.foes.Add(foe);
        }
        return simulation;
    }

    private static DungeonPathState CapturePath(PathFollower path) =>
        new(path.Goal is { } goal ? new TilePoint(goal.X, goal.Y) : null, path.Waypoints.ToArray());

    private static void RestorePath(PathFollower path, DungeonPathState state)
    {
        path.Goal = state.Goal is { } goal ? ((int)goal.X, (int)goal.Y) : null;
        foreach (var point in state.Waypoints) path.Waypoints.Enqueue(point);
    }
}

/// <summary>SplitMix64: explicit random state survives process restart, independently of System.Random internals.</summary>
public sealed class DungeonRandom(ulong state) : Random
{
    public ulong State { get; private set; } = state;
    public static DungeonRandom FromSeed(int seed) => new(unchecked((ulong)(long)seed));

    private ulong NextBits()
    {
        State = unchecked(State + 0x9E3779B97F4A7C15UL);
        var value = State;
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
        return value ^ (value >> 31);
    }

    public override double NextDouble() => (NextBits() >> 11) * (1.0 / (1UL << 53));
    protected override double Sample() => NextDouble();
    public override int Next() => Next(int.MaxValue);
    public override int Next(int maxValue) => Next(0, maxValue);
    public override int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxValue, minValue);
        var range = (ulong)((long)maxValue - minValue);
        if (range == 0) return minValue;
        // Reject the short tail instead of biasing small loot ranges with a modulo.
        var threshold = unchecked(0UL - range) % range;
        ulong value;
        do { value = NextBits(); } while (value < threshold);
        return (int)(minValue + (long)(value % range));
    }
}
