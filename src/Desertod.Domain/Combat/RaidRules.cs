using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;

namespace Desertod.Domain.Combat;

/// <param name="Renown">Renown the defender earned for the slain enemies.</param>
/// <param name="Broken">Weapon or armor that broke in the fight.</param>
public sealed record RaidDefenderReport(Guid CitizenId, string Name, bool Injured, int Renown, IReadOnlyList<ResourceKind> Broken);

/// <summary>A citizen who was at the camp, not on watch, and got hurt when the raiders broke in.</summary>
public sealed record RaidVictimReport(Guid CitizenId, string Name);

public sealed record RaidReport(
    Guid RaidId,
    RaidSource Source,
    EnemyKind Enemy,
    int Count,
    int Wave,
    int Waves,
    DateTimeOffset StrikesAt,
    bool Repelled,
    int Killed,
    double Defense,
    double Attack,
    IReadOnlyList<RaidDefenderReport> Defenders,
    IReadOnlyList<RaidVictimReport> Victims,
    IReadOnlyList<ResourceAmount> Loot,
    IReadOnlyList<ResourceAmount> Stolen,
    IReadOnlyList<string> DamagedPlots,
    double MoraleChange) { public int EngineVersion { get; init; } public RaidSummary? Summary { get; init; } public RaidOutcome? Outcome { get; init; } }

/// <summary>
/// Attacks on the camp. Some nights beasts come out of the woods; the Tsar can call the dead up for the viewers.
/// Citizens on a guard task when the attack strikes defend the camp: their fight power, the campfire, the palisade and
/// the barracks make the defense. If it beats the attackers' power the raid is repelled and the slain leave loot.
/// Otherwise the survivors steal goods, damage buildings and wound defenders and citizens resting at the camp.
/// </summary>
public static class RaidRules
{
    /// <summary>Chance that beasts attack on a night.</summary>
    public const double NightRaidChance = 0.35;

    /// <summary>Part of the night the beasts strike in: never right at dusk or dawn.</summary>
    public const double EarliestStrike = 0.15;

    public const double LatestStrike = 0.85;

    /// <summary>Night raids are seen coming this long before they strike, at most; shorter on very short nights.</summary>
    public static TimeSpan Warning { get; } = TimeSpan.FromMinutes(15);

    /// <summary>The undead rise this long after the Tsar calls them, so citizens can take up arms.</summary>
    public static TimeSpan UndeadWarning { get; } = TimeSpan.FromMinutes(3);

    public static TimeSpan WaveInterval { get; } = TimeSpan.FromMinutes(2);

    /// <summary>Resolved raids kept for the record.</summary>
    public const int KeptRaids = 10;

    /// <summary>Beasts fear the fire; even an empty camp puts up this much defense.</summary>
    public const double CampfireDefense = 3;

    public const double PalisadeDefense = 5;
    public const double PalisadeMultiplier = 1.25;
    public const double BarracksMultiplier = 1.1;

    /// <summary>A captain on command adds this much to the whole defense per point of Endurance.</summary>
    public const double CommandPerEndurance = 0.04;

    /// <summary>Luck of the fight: the defense varies this much either way.</summary>
    public const double Luck = 0.25;

    public const double RepelledInjuryChance = 0.15;
    public const double LostInjuryChance = 0.6;

    /// <summary>Chance that a raider who broke in wounds a citizen resting at the camp.</summary>
    public const double VictimInjuryChance = 0.5;

    public const int RenownPerKill = 2;
    public const double RepelledMorale = 0.03;
    public const double LostMorale = -0.08;
    public const string CaptainRole = "captain";

    public static IEnumerable<Raid> Pending(Village village) => village.Raids.Where(r => r.IsPending).OrderBy(r => r.StrikesAt);

    /// <summary>Raids that struck by now and are not fought yet, oldest first.</summary>
    public static IReadOnlyList<Raid> Due(Village village, DateTimeOffset now) =>
        Pending(village).Where(r => r.StrikesAt <= now).ToList();

    public static void UpdateNextRaidAt(Village village) =>
        village.NextRaidAt = Pending(village).Select(r => (DateTimeOffset?)r.StrikesAt).FirstOrDefault();

    /// <summary>
    /// Decides once per night whether beasts attack, and when. Called during the night; a strike too late to be seen
    /// coming is not planned. New villages and villages nobody works in are left alone.
    /// </summary>
    /// <param name="activeCitizens">Citizens who worked lately; the pack grows with the village.</param>
    public static Raid? PlanNight(Village village, int activeCitizens, Random random, DateTimeOffset now)
    {
        var period = DayCycle.PeriodOf(now);
        if (village.RaidsPlannedPeriod >= period || !DayCycle.IsNight(period))
        {
            return null;
        }

        village.RaidsPlannedPeriod = period;
        if (activeCitizens == 0 || CombatRules.IsPeaceful(village, now) || Pending(village).Any(r => r.Source == RaidSource.Night)
            || random.NextDouble() >= NightRaidChance)
        {
            return null;
        }

        var start = DayCycle.PeriodStart(period);
        var warning = Warning < DayCycle.PeriodLength * EarliestStrike ? Warning : DayCycle.PeriodLength * EarliestStrike;
        var earliest = Max(start + DayCycle.PeriodLength * EarliestStrike, now + warning);
        var latest = start + DayCycle.PeriodLength * LatestStrike;
        if (earliest >= latest)
        {
            return null;
        }

        var enemy = Beast(TechnologyRules.EraOf(village), random);
        var raid = new Raid
        {
            Source = RaidSource.Night,
            Enemy = enemy,
            Count = BeastCount(enemy, activeCitizens),
            StrikesAt = earliest + (latest - earliest) * random.NextDouble(),
        };
        raid.AnnouncedAt = raid.StrikesAt - warning;
        village.Raids.Add(raid);
        UpdateNextRaidAt(village);
        return raid;
    }

    /// <summary>The Tsar calls the dead up: one wave, or more for a bigger event. One event at a time.</summary>
    public static IReadOnlyList<Raid> CallUndead(Village village, EnemyKind kind, RaidSize size, int activeCitizens, DateTimeOffset now)
    {
        if (EnemyCatalog.Get(kind) is not { Undead: true, DungeonOnly: false })
        {
            throw new GameRuleException("raid.notUndead", "Only the undead answer the Tsar's call.");
        }

        if (Pending(village).Any(r => r.Source == RaidSource.Undead))
        {
            throw new GameRuleException("raid.undeadComing", "The dead are already on their way. Wait until the last wave is fought.");
        }

        var (perWave, waves) = size switch
        {
            RaidSize.Small => (3, 1),
            RaidSize.Medium => (4, 2),
            _ => (6, 3),
        };
        var raids = new List<Raid>();
        for (var wave = 1; wave <= waves; wave++)
        {
            var count = perWave + wave - 1 + activeCitizens / 2;
            raids.Add(new Raid
            {
                Source = RaidSource.Undead,
                Enemy = kind,
                Count = (int)Math.Ceiling(count * EnemyCatalog.Get(kind).RaidCountMultiplier),
                Wave = wave,
                Waves = waves,
                AnnouncedAt = now,
                StrikesAt = now + UndeadWarning + WaveInterval * (wave - 1),
            });
        }

        village.Raids.AddRange(raids);
        UpdateNextRaidAt(village);
        return raids;
    }

    /// <summary>The Tsar sends the dead back before they strike.</summary>
    public static void CancelUndead(Village village)
    {
        if (village.Raids.RemoveAll(r => r.IsPending && r.Source == RaidSource.Undead) == 0)
        {
            throw new GameRuleException("raid.noUndead", "No undead are on their way.");
        }

        UpdateNextRaidAt(village);
    }

    /// <summary>The guard task a citizen was on when the raid struck, or null if they were not on watch.</summary>
    public static TaskDefinition? GuardTask(Citizen citizen, DateTimeOffset strikesAt) =>
        citizen.Status == CitizenStatus.Active
        && citizen.RestState != RestState.Resting
        && citizen.RecoveryPausedAt is null && !HospitalRules.IsAdmitted(citizen, strikesAt)
        && !citizen.CycleSettled
        && citizen.CurrentSiteId is null
        && citizen.CycleStartedAt <= strikesAt
        && strikesAt < citizen.CycleEndsAt
        && citizen.RecoveryPausedAt is null
        && citizen.Recovery?.Available != false
        && !(citizen.Recovery?.Until > strikesAt)
        && !CombatRules.IsInjured(citizen, strikesAt)
        && GameCatalog.FindTask(citizen.CurrentTaskId ?? "") is { Effect: TaskEffect.Guard } task
            ? task
            : null;

    /// <summary>Resting at the camp when the raid struck: not working and not hurt already.</summary>
    public static bool AtRest(Citizen citizen, DateTimeOffset strikesAt) =>
        citizen.Status == CitizenStatus.Active
        && citizen.RestState != RestState.Resting
        && citizen.RecoveryPausedAt is null && !HospitalRules.IsAdmitted(citizen, strikesAt)
        && !CombatRules.IsInjured(citizen, strikesAt)
        && (citizen.CycleStartedAt is not { } started || started > strikesAt || citizen.CycleEndsAt is not { } ends || ends <= strikesAt);

    /// <summary>Total defense: the watchers' fight power, the fire, the palisade, the barracks and a captain's command.</summary>
    public static double Defense(Village village, IReadOnlyList<(Citizen Citizen, TaskDefinition Task)> defenders, IReadOnlyCollection<string> completedProjects, Raid? raid = null)
    {
        var palisade = completedProjects.Contains(ProjectCatalog.Palisade);
        var command = defenders.Where(d => d.Task.RoleId == CaptainRole)
            .Select(d => 1 + CommandPerEndurance * d.Citizen.Stats.Endurance)
            .DefaultIfEmpty(1)
            .Max();
        var strength = defenders.Sum(d => CombatRules.Power(d.Citizen) * d.Task.BaseYield * (raid?.EntrySector is not null && string.IsNullOrEmpty(d.Citizen.DefenseSector) ? .75 : 1));
        return (strength + CampfireDefense + (palisade ? PalisadeDefense : 0))
               * command
               * (palisade ? PalisadeMultiplier : 1)
               * (village.Has(BuildingKind.Barracks) ? BarracksMultiplier : 1);
    }

    /// <summary>Fights the raid out with the citizens on watch at the moment it struck.</summary>
    /// <param name="citizens">Every citizen of the village; the watchers and those resting at the camp are picked from them.</param>
    public static RaidReport Resolve(
        Village village,
        Raid raid,
        IReadOnlyList<Citizen> citizens,
        IReadOnlyCollection<string> completedProjects,
        Random random,
        DateTimeOffset now)
    {
        var enemy = EnemyCatalog.Get(raid.Enemy);
        var defenders = citizens
            .Select(c => (Citizen: c, Task: GuardTask(c, raid.StrikesAt)))
            .Where(d => d.Task is not null && RaidTactics.CanDefend(d.Citizen, raid))
            .Select(d => (d.Citizen, Task: d.Task!))
            .ToList();
        var defense = Defense(village, defenders, completedProjects, raid);
        var attack = raid.Count * enemy.Power;
        var ratio = defense * (1 + Luck * (2 * random.NextDouble() - 1)) / attack;
        var repelled = ratio >= 1;
        var killed = repelled
            ? Math.Clamp((int)Math.Round(raid.Count * Math.Min(1, ratio - 0.5)), 1, raid.Count)
            : Math.Min(raid.Count - 1, (int)Math.Floor(raid.Count * ratio * 0.5));
        var survivors = raid.Count - killed;

        var renownEach = repelled && defenders.Count > 0 ? (int)Math.Ceiling((double)RenownPerKill * killed / defenders.Count) : 0;
        var defenderReports = new List<RaidDefenderReport>();
        foreach (var (citizen, _) in defenders)
        {
            var injured = CombatRules.Wound(citizen, repelled ? RepelledInjuryChance : LostInjuryChance, enemy, random, now);
            var broken = CombatRules.Wear(citizen, random);
            SkillRules.AddExperience(citizen, Skill.Guarding, repelled ? CombatRules.FightExperience : CombatRules.FightExperience / 2);
            citizen.Renown += renownEach;
            defenderReports.Add(new RaidDefenderReport(citizen.Id, citizen.Name, injured, renownEach, broken));
        }

        var victims = new List<RaidVictimReport>();
        var stolen = new List<ResourceAmount>();
        var damaged = new List<string>();
        if (!repelled)
        {
            var resting = citizens.Where(c => AtRest(c, raid.StrikesAt)).OrderBy(_ => random.Next()).Take(survivors);
            foreach (var citizen in resting)
            {
                if (CombatRules.Wound(citizen, VictimInjuryChance, enemy, random, now))
                {
                    victims.Add(new RaidVictimReport(citizen.Id, citizen.Name));
                }
            }

            foreach (var good in enemy.Steals)
            {
                var taken = village.Take(good.Resource, good.Amount * survivors);
                if (taken > 0)
                {
                    stolen.Add(new ResourceAmount(good.Resource, taken));
                }
            }

            var buildings = CombatRules.Buildings(village).Select(b => b.PlotId).ToList();
            for (var raider = 0; raider < survivors && enemy.Damage > 0 && buildings.Count > 0; raider++)
            {
                var plotId = buildings[random.Next(buildings.Count)];
                if (CombatRules.Damage(village, plotId, enemy.Damage) > 0 && !damaged.Contains(plotId))
                {
                    damaged.Add(plotId);
                }
            }
        }

        var loot = new List<ResourceAmount>();
        foreach (var good in enemy.Loot)
        {
            var stored = village.Store(good.Resource, good.Amount * killed);
            if (stored > 0)
            {
                loot.Add(new ResourceAmount(good.Resource, stored));
            }
        }

        var moraleBefore = village.MoraleAt(now);
        village.AddMorale(repelled ? RepelledMorale : LostMorale, now);

        raid.Outcome = repelled ? RaidOutcome.Repelled : RaidOutcome.Lost;
        raid.ResolvedAt = now;
        raid.Killed = killed;
        raid.Defenders = defenders.Count;
        raid.Injured = defenderReports.Count(d => d.Injured) + victims.Count;
        raid.DamagedBuildings = damaged.Count;
        raid.Stolen = stolen.Select(s => new RaidGood { Kind = s.Resource, Amount = s.Amount }).ToList();
        raid.Loot = loot.Select(l => new RaidGood { Kind = l.Resource, Amount = l.Amount }).ToList();

        var resolved = village.Raids.Where(r => !r.IsPending).OrderByDescending(r => r.ResolvedAt).Skip(KeptRaids).ToList();
        village.Raids.RemoveAll(resolved.Contains);
        UpdateNextRaidAt(village);

        return new RaidReport(raid.Id, raid.Source, raid.Enemy, raid.Count, raid.Wave, raid.Waves, raid.StrikesAt, repelled, killed,
            Math.Round(defense, 1), attack, defenderReports, victims, loot, stolen, damaged, village.Morale - moraleBefore);
    }

    /// <summary>Wolves come first; bigger beasts follow as the village grows and its smell carries farther.</summary>
    private static EnemyKind Beast(Era era, Random random)
    {
        var roll = random.NextDouble();
        return era switch
        {
            Era.Wild or Era.Camp => EnemyKind.Wolf,
            Era.Village => roll < 0.7 ? EnemyKind.Wolf : EnemyKind.Boar,
            _ => roll < 0.45 ? EnemyKind.Wolf : roll < 0.8 ? EnemyKind.Boar : EnemyKind.Bear,
        };
    }

    private static int BeastCount(EnemyKind kind, int activeCitizens) => kind switch
    {
        EnemyKind.Wolf => Math.Clamp(2 + activeCitizens / 2, 2, 12),
        EnemyKind.Boar => Math.Clamp(1 + activeCitizens / 3, 1, 8),
        _ => Math.Clamp(1 + activeCitizens / 8, 1, 3),
    };

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
