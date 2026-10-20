using System.Collections.Concurrent;
using Desertod.Domain.Dungeons;

namespace Desertod.Api.Game;

/// <summary>A fight under way, held in memory. Every access to <see cref="Simulation"/> takes <see cref="Gate"/>.</summary>
public sealed class LiveDungeon(DungeonStart start)
{
    public Guid RunId { get; } = start.RunId;
    public Guid VillageId { get; } = start.VillageId;
    public string Slug { get; } = start.Slug;
    public DungeonDefinition Dungeon { get; } = start.Dungeon;
    public DungeonSimulation Simulation { get; private set; } = start.Save is { } saved
        ? DungeonSimulation.Restore(saved.Fight) : new(start.Dungeon, start.Fighters);
    public Random Random { get; private set; } = start.Save is { } randomSave
        ? new DungeonRandom(randomSave.RandomState) : new Random(start.Seed);
    public DungeonSave? Save { get; private set; } = start.Save;

    /// <summary>Only one durable tick or command per run. Never held while waiting on another run.</summary>
    public SemaphoreSlim Operation { get; } = new(1, 1);
    public long LastTickTimestamp { get; set; }
    public IReadOnlyList<DungeonRunHeroDto> Heroes { get; } = start.Heroes;

    /// <summary>Hero id to the account that plays them.</summary>
    public IReadOnlyDictionary<Guid, Guid> Owners { get; } = start.Owners;

    public Lock Gate { get; } = new();

    /// <summary>Heroes already handed over for settling, so each leaves once.</summary>
    public HashSet<Guid> Settled { get; } = start.Save?.Settled.ToHashSet() ?? [];

    public DungeonSnapshotDto? LastSnapshot { get; set; }

    public void Commit(DungeonSave save, DungeonSimulation simulation, DungeonRandom random, DungeonSnapshotDto snapshot)
    {
        lock (Gate)
        {
            Save = save;
            Simulation = simulation;
            Random = random;
            Settled.UnionWith(save.Settled);
            LastSnapshot = snapshot;
        }
    }

    public bool Owns(Guid heroId, Guid accountId) => Owners.TryGetValue(heroId, out var owner) && owner == accountId;

    public Guid? HeroOf(Guid accountId) => Owners.Where(o => o.Value == accountId).Select(o => (Guid?)o.Key).FirstOrDefault();
}

/// <summary>The fights under way in this process; the server runs as a single instance.</summary>
public sealed class DungeonRuntime
{
    private readonly ConcurrentDictionary<Guid, LiveDungeon> runs = new();

    public IEnumerable<LiveDungeon> Runs => runs.Values;

    public LiveDungeon Start(DungeonStart start) => runs.GetOrAdd(start.RunId, _ => new LiveDungeon(start));

    public LiveDungeon? Find(Guid runId) => runs.GetValueOrDefault(runId);

    public void Remove(Guid runId) => runs.TryRemove(runId, out _);
}
