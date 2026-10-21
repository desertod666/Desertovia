using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;

namespace Desertod.Domain.Dungeons;

public enum DungeonRunKind { Group, Ambush }

public enum DungeonPhase
{
    /// <summary>The Tsar called; citizens answer until <see cref="DungeonRun.GatheringEndsAt"/>.</summary>
    Gathering,

    /// <summary>The heroes walk to the edge of the land.</summary>
    Marching,

    /// <summary>The fight on the dungeon map.</summary>
    Running,

    Ended,
}

public enum DungeonOutcome
{
    Pending,

    /// <summary>Every foe slain.</summary>
    Victory,

    /// <summary>Every hero out, or the time ran out.</summary>
    Defeat,

    /// <summary>The server stopped during the run.</summary>
    Abandoned,

    /// <summary>The Tsar called it off while gathering.</summary>
    Cancelled,
}

/// <summary>One trip of the tribe into a dungeon, from the Tsar's call to the end of the fight.</summary>
public sealed class DungeonRun
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public DungeonRunKind Kind { get; set; }
    public Guid? WorkCitizenId { get; set; }
    public DateTimeOffset? WorkStartedAt { get; set; }
    public string? WorkTaskId { get; set; }
    public EnemyKind? AmbushEnemy { get; set; }
    public required string DungeonId { get; set; }
    public DungeonPhase Phase { get; set; }
    public DungeonOutcome Outcome { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset GatheringEndsAt { get; set; }

    /// <summary>The fight starts then: the last hero has reached the edge of the land, or the march took too long.</summary>
    public DateTimeOffset? MarchEndsAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Seeds the fight, so it plays out the same from the same intents.</summary>
    public int Seed { get; set; }

    public int Slain { get; set; }
    public int Foes { get; set; }

    /// <summary>Versioned full fight state, saved atomically with exits and rewards. Null for pre-checkpoint runs.</summary>
    public string? CheckpointJson { get; set; }

    public List<DungeonHero> Heroes { get; set; } = [];

    public bool IsActive => Phase != DungeonPhase.Ended;

    public DungeonHero? Hero(Guid citizenId) => Heroes.FirstOrDefault(h => h.CitizenId == citizenId);

    public DungeonHero Tsar => Heroes.First(h => h.Class == HeroClass.Voivode);
}

public sealed class DungeonHero
{
    /// <summary>Null for the Tsar, who is no citizen.</summary>
    public Guid? CitizenId { get; set; }

    public Guid AccountId { get; set; }
    public required string Name { get; set; }
    public HeroClass Class { get; set; }

    /// <summary>Reaches the edge of the land then, on the march.</summary>
    public DateTimeOffset? ArrivesAt { get; set; }

    /// <summary>Taken from storage for the run; an archer's arrows and a healer's remedy.</summary>
    public int Arrows { get; set; }

    public int Remedies { get; set; }

    /// <summary>Fell in the fight; <see cref="Killer"/> decides the wound.</summary>
    public bool KnockedOut { get; set; }

    public EnemyKind? Killer { get; set; }

    /// <summary>Back in the village with rewards, wound and gear sorted out.</summary>
    public bool Settled { get; set; }

    public int Kills { get; set; }
    public int Experience { get; set; }

    /// <summary>Goods the hero brought back to the village.</summary>
    public List<DungeonGood> Loot { get; set; } = [];

    /// <summary>Gear that broke on the way.</summary>
    public List<ResourceKind> Broken { get; set; } = [];

    public bool IsTsar => CitizenId is null;
}

public sealed class DungeonGood
{
    public ResourceKind Kind { get; set; }
    public int Amount { get; set; }
}
