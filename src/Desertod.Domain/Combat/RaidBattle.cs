using Desertod.Domain.Catalog;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <summary>A separate aggregate: frequent unit writes do not reindex the village's construction JSON.</summary>
public sealed class RaidBattle
{
    public Guid Id { get; set; }
    public Guid VillageId { get; set; }
    public int EngineVersion { get; set; } = 2;
    /// <summary>0 for announced legacy raids; new battles use the common mortal body rules.</summary>
    public int CombatRulesVersion { get; set; }
    public string Phase { get; set; } = "Warning";
    public required string Sector { get; set; }
    public string Tactic { get; set; } = "entry";
    public string Composition { get; set; } = "mixed";
    public RaidSettings? Settings { get; set; }
    public Guid? EventId { get; set; }
    public Guid? InitiatedBy { get; set; }
    public bool ManualOverride { get; set; }
    public DateTimeOffset? FirstContactAt { get; set; }
    public DateTimeOffset? LastContactAt { get; set; }
    public double PlayerHealthLost { get; set; }
    public List<RaidLootBudget> LootBudgets { get; set; } = [];
    public int RemediesUsed { get; set; }
    public string? Result { get; set; }
    public int Seed { get; set; }
    public double ThreatBudget { get; set; }
    public int ReadyDefenders { get; set; }
    public int AvailableReserves { get; set; }
    // Immutable queue order; StartsAt may move when an earlier wave takes longer.
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset LastTickAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long Revision { get; set; }
    public bool Deferred { get; set; }
    public double DamageDone { get; set; }
    public List<RaidUnit> Units { get; set; } = [];
    public List<RaidFighter> Fighters { get; set; } = [];
    public List<RaidDamage> Damage { get; set; } = [];
    public List<RaidBattleEvent> Events { get; set; } = [];
    public void Event(string kind, string target, DateTimeOffset at)
    {
        if (Events.Count < 160) Events.Add(new() { Sequence = Events.Count + 1, Kind = kind, Target = target, At = at });
    }
}

public sealed class RaidUnit
{
    public EnemyKind? Enemy { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Variant { get; set; } = "regular";
    public double X { get; set; }
    public double Y { get; set; }
    public double SpawnX { get; set; }
    public double SpawnY { get; set; }
    public double Health { get; set; }
    public double MaximumHealth { get; set; }
    public double Attack { get; set; }
    public double Speed { get; set; }
    public double Range { get; set; } = 1.15;
    public string State { get; set; } = "Waiting";
    public string? TargetKind { get; set; }
    public string? TargetId { get; set; }
    public string? PlunderPlot { get; set; }
    public DateTimeOffset NextAttackAt { get; set; }
    public DateTimeOffset? DiedAt { get; set; }
    public DateTimeOffset? ExitedAt { get; set; }
    public double BlockedSeconds { get; set; }
    // Nullable: JSON старого активного боя не содержит этих ключей.
    public int? BossPhase { get; set; }
    public double? StrikeX { get; set; }
    public double? StrikeY { get; set; }
    public string? StrikeTarget { get; set; }
    public DateTimeOffset? StrikeAt { get; set; }
    public DateTimeOffset? SpecialReadyAt { get; set; }
    public TilePoint Position => new(X, Y);
    public bool Alive => Health > 0 && ExitedAt is null;
}

public sealed class RaidFighter
{
    /// <summary>Frozen when admitted and carried with the body; missing legacy JSON retains its original rules.</summary>
    public int? CombatRulesVersion { get; set; }
    public Guid CitizenId { get; set; }
    public Guid? AccountId { get; set; }
    public bool IsTsar { get; set; }
    public Guid ActorId => IsTsar ? AccountId ?? Guid.Empty : CitizenId;
    public double? DestinationX { get; set; }
    public double? DestinationY { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Health { get; set; }
    public double MaximumHealth { get; set; }
    public double DamageDealt { get; set; }
    public double? RangedDamage { get; set; }
    public double DamageTaken { get; set; }
    public int Kills { get; set; }
    public int RenownEarned { get; set; }
    public ResourceKind? Weapon { get; set; }
    public ResourceKind? Armor { get; set; }
    public int Arrows { get; set; }
    public int ArrowsSpent { get; set; }
    public bool Wounded { get; set; }
    public bool Guard { get; set; }
    public bool Active { get; set; }
    public bool RewardsGranted { get; set; }
    public DateTimeOffset? HealReadyAt { get; set; }
    public string? HealingReceipts { get; set; }
    public string Mode { get; set; } = "intercept";
    public Guid? TargetUnitId { get; set; }
    public DateTimeOffset NextAttackAt { get; set; }
    public TilePoint Position => new(X, Y);
}

public sealed class RaidDamage
{
    public required string PlotId { get; set; }
    public int Amount { get; set; }
}

public sealed class RaidBattleEvent
{
    public int Sequence { get; set; }
    public required string Kind { get; set; }
    public required string Target { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class RaidLootBudget
{
    public required string PlotId { get; set; }
    public ResourceKind Kind { get; set; }
    public int Limit { get; set; }
    public int Taken { get; set; }
}
public sealed class RaidSummary
{
    public double ContactSeconds { get; set; }
    public int Participants { get; set; }
    public double PlayerHealthLost { get; set; }
    public int RemediesUsed { get; set; }
    public int ArrowsSpent { get; set; }
    public int BrokenItems { get; set; }
    public int PropertyDamage { get; set; }
    public int WallDamage { get; set; }
    public double NextCoefficient { get; set; }
    public string Reason { get; set; } = "";
}
