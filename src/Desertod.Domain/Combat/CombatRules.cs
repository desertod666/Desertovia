using Desertod.Domain.Economy;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <param name="Power">Fight power of the citizen, weapon included.</param>
/// <param name="EnemyPower">Fight power of the enemy.</param>
/// <param name="Loot">What the slain enemy left; stored like any other produce.</param>
/// <param name="Broken">Weapon or armor that broke in the fight.</param>
public sealed record FightReport(
    EnemyKind Enemy,
    bool Won,
    bool Injured,
    DateTimeOffset? InjuredUntil,
    double Power,
    double EnemyPower,
    ResourceKind? Weapon,
    ResourceKind? Armor,
    IReadOnlyList<ResourceAmount> Loot,
    IReadOnlyList<ResourceKind> Broken);

/// <summary>
/// Fights between citizens and beasts or the undead. A fight is won with the chance power / (power + enemy power);
/// a loss wounds the citizen unless their armor stops the blow. A wounded citizen cannot work until healed.
/// </summary>
public static class CombatRules
{
    public const double UnarmedPower = 2;
    public const double StrengthPower = 0.6;
    public const double DexterityPower = 0.4;
    public const double GuardingPower = 0.3;

    /// <summary>Beasts hunt in the dark: ambushes are this much more likely at night.</summary>
    public const double NightAmbushMultiplier = 1.5;

    /// <summary>Even a won fight may leave a wound.</summary>
    public const double WonInjuryChance = 0.1;

    /// <summary>A building at full damage works at half speed; see <see cref="BuildingMultiplier"/>.</summary>
    public const int MaximumDamage = 100;

    /// <summary>Skill experience for a fight: the same as a cycle of work, half of it for a lost fight.</summary>
    public const int FightExperience = GameRules.BaseExperiencePerCycle;

    /// <summary>A new village is left alone this long, so its first players learn the game in peace.</summary>
    public static TimeSpan PeacefulTime { get; private set; } = TimeSpan.FromDays(1);

    public static void Configure(TimeSpan peacefulTime) =>
        PeacefulTime = peacefulTime >= TimeSpan.Zero
            ? peacefulTime
            : throw new ArgumentOutOfRangeException(nameof(peacefulTime), peacefulTime, "The peaceful time cannot be negative.");

    public static bool IsPeaceful(Village village, DateTimeOffset now) => now < village.CreatedAt + PeacefulTime;

    public static bool IsInjured(Citizen citizen, DateTimeOffset now) => citizen.InjuredUntil is { } until && now < until;

    /// <summary>Fight power: strength and agility, training as a guard, and the weapon in hand.</summary>
    public static double Power(Citizen citizen) =>
        UnarmedPower
        + StrengthPower * citizen.Stats.Strength
        + DexterityPower * citizen.Stats.Dexterity
        + GuardingPower * citizen.SkillLevel(Skill.Guarding)
        + (UsableWeapon(citizen) is { } weapon
            ? weapon.Power + WeaponSharpening.Bonus(weapon.Kind == ResourceKind.Bow ? citizen.ArrowSharpen : citizen.WeaponSharpen)
            : 0);

    /// <summary>The weapon in hand, if it can be used: a bow needs arrows.</summary>
    public static WeaponDefinition? UsableWeapon(Citizen citizen) =>
        ArmoryCatalog.Weapon(citizen.Weapon) is { } weapon && citizen.Arrows >= weapon.Arrows ? weapon : null;

    /// <summary>
    /// Takes the best weapon and armor from the citizen's hands or village storage. A citizen who already holds
    /// a weapon or armor (because storage had no room to take it back) keeps it.
    /// </summary>
    public static void Arm(Citizen citizen, Village village)
    {
        if (citizen.Weapon is null && BestWeapon(citizen, village) is { } best)
        {
            Take(citizen, village, best.Weapon.Kind, 1, best.Sharpen);
            citizen.Weapon = best.Weapon.Kind;
            citizen.WeaponSharpen = best.Sharpen;
            if (best.Weapon.Arrows > 0)
            {
                FillQuiver(citizen, village, best.ArrowEdge);
            }
        }

        if (citizen.Armor is null && ArmoryCatalog.Armors.FirstOrDefault(a => Available(citizen, village, a.Kind) > 0) is { } armor)
        {
            TakeOne(citizen, village, armor.Kind);
            citizen.Armor = armor.Kind;
        }
    }

    /// <summary>Work completion must not disarm a citizen during a live raid. Ordinary return is unchanged outside combat.</summary>
    public static void ReturnWorkEquipment(Citizen citizen, Village village, DateTimeOffset now)
    {
        if (!village.Raids.Any(r => r.IsPending && r.StrikesAt <= now)) Disarm(citizen, village);
    }

    /// <summary>Puts weapon, armor and arrows back into storage. What does not fit stays with the citizen.</summary>
    public static void Disarm(Citizen citizen, Village village)
    {
        if (citizen.Weapon is { } weapon && village.Store(weapon, 1, citizen.WeaponSharpen) == 1)
        {
            citizen.ClearWeapon();
        }

        if (citizen.Armor is { } armor && village.Store(armor, 1) == 1)
        {
            citizen.Armor = null;
        }

        if (citizen.Arrows > 0)
        {
            citizen.Arrows -= village.Store(ResourceKind.Arrows, citizen.Arrows, citizen.ArrowSharpen);
            if (citizen.Arrows == 0)
            {
                citizen.ArrowSharpen = 0;
            }
        }
    }

    /// <summary>
    /// A beast may jump at a citizen working out in the wild. New villages are spared; see <see cref="PeacefulTime"/>.
    /// Returns null when nothing attacked.
    /// </summary>
    /// <param name="startedAt">When the cycle started: its daylight decides the danger.</param>
    public static FightReport? Ambush(Citizen citizen, Village village, DangerDefinition danger, Random random, DateTimeOffset startedAt, DateTimeOffset now)
    {
        var enemy=SelectAmbush(village,danger,random,startedAt);
        return enemy is null?null:Fight(citizen,enemy,random,now);
    }

    /// <summary>Selects a world encounter without resolving its fight, spending equipment or granting rewards.</summary>
    public static EnemyDefinition? SelectAmbush(Village village,DangerDefinition danger,Random random,DateTimeOffset startedAt)
    {
        if (IsPeaceful(village, startedAt) || danger.Enemies.Count == 0)
        {
            return null;
        }

        var chance = danger.Chance * (DayCycle.IsNight(startedAt) ? NightAmbushMultiplier : 1);
        if (random.NextDouble() >= chance)
        {
            return null;
        }

        var enemies = danger.Enemies.Select(EnemyCatalog.Get).ToList();
        var roll = random.Next(enemies.Sum(e => Math.Max(1, e.AmbushWeight)));
        var enemy = enemies[^1];
        foreach (var candidate in enemies)
        {
            var weight = Math.Max(1, candidate.AmbushWeight);
            if (roll < weight)
            {
                enemy = candidate;
                break;
            }

            roll -= weight;
        }

        return enemy;
    }

    /// <summary>One citizen against one enemy. Trains Guarding; the loot is for the caller to store.</summary>
    public static FightReport Fight(Citizen citizen, EnemyDefinition enemy, Random random, DateTimeOffset now)
    {
        var power = Power(citizen);
        var won = random.NextDouble() < power / (power + enemy.Power);
        var weapon = UsableWeapon(citizen)?.Kind;
        var armor = citizen.Armor;
        var injured = Wound(citizen, won ? WonInjuryChance : 1, enemy, random, now);
        var broken = Wear(citizen, random);
        SkillRules.AddExperience(citizen, Skill.Guarding, won ? FightExperience : FightExperience / 2);
        return new FightReport(enemy.Kind, won, injured, injured ? citizen.InjuredUntil : null, power, enemy.Power, weapon, armor,
            won ? enemy.Loot : [], broken);
    }

    /// <summary>Wounds the citizen with this chance, unless their armor stops the blow. Returns true if wounded.</summary>
    public static bool Wound(Citizen citizen, double chance, EnemyDefinition enemy, Random random, DateTimeOffset now)
    {
        var protection = ArmoryCatalog.Armor(citizen.Armor)?.Protection ?? 0;
        if (random.NextDouble() >= chance * (1 - protection))
        {
            return false;
        }

        Injure(citizen, TimeSpan.FromMinutes(enemy.InjuryMinutes), now, rewardEligible: true);
        return true;
    }

    public static void Injure(Citizen citizen, TimeSpan duration, DateTimeOffset now, bool rewardEligible = false)
    {
        var until = now + duration;
        if (citizen.InjuredUntil is not { } current || current < until)
        {
            citizen.InjuredUntil = until;
            citizen.IdleSpot=citizen.Spot??citizen.Trip?.To??citizen.IdleSpot;
            if (!citizen.CycleSettled && citizen.CycleEndsAt > now) citizen.RecoveryPausedAt ??= now;
            citizen.WoundedAt = now;citizen.HospitalizedAt = null;citizen.RescueByCitizenId=null;
            citizen.RescueRewardEligible = rewardEligible;
            citizen.RescueRequestedAt = null;
        }
    }

    /// <summary>A fight wears weapons down: each may break, and a bow shoots its arrows. Returns what broke.</summary>
    public static IReadOnlyList<ResourceKind> Wear(Citizen citizen, Random random)
    {
        var broken = new List<ResourceKind>();
        if (UsableWeapon(citizen) is { } weapon)
        {
            citizen.SpendArrows(weapon.Arrows);
            if (random.NextDouble() < weapon.BreakChance)
            {
                broken.Add(weapon.Kind);
                citizen.ClearWeapon();
            }
        }

        if (ArmoryCatalog.Armor(citizen.Armor) is { } armor && random.NextDouble() < armor.BreakChance)
        {
            broken.Add(armor.Kind);
            citizen.Armor = null;
        }

        return broken;
    }

    /// <summary>A remedy closes the wound at once. It is taken from the citizen's hands first, then from storage.</summary>
    public static void Heal(Citizen citizen, Village village, DateTimeOffset now)
    {
        if (!IsInjured(citizen, now))
        {
            throw new GameRuleException("heal.notInjured", "You are not wounded.");
        }

        if (PersonalInventory.Amount(citizen, ResourceKind.Remedy) == 0 && Available(citizen, village, ResourceKind.Remedy) == 0)
        {
            throw new GameRuleException("heal.none", "There is no remedy in storage. A healer must make some first.");
        }

        if (PersonalInventory.Amount(citizen, ResourceKind.Remedy) > 0) PersonalInventory.Remove(citizen, ResourceKind.Remedy, 1);
        else TakeOne(citizen, village, ResourceKind.Remedy);
        citizen.InjuredUntil = null;citizen.RescueByCitizenId=null;
        HospitalRules.Resume(citizen, now, now);
    }

    /// <summary>Collapsed workplaces cannot start work; paid cycles settle without losing their inputs.</summary>
    public static double BuildingMultiplier(Village village, TaskDefinition task) =>
        task.RequiredBuilding is { } kind && village.Structures.FirstOrDefault(s => s.Kind == kind) is { } building
            ? (building.Damage >= MaximumDamage ? 0 : 1)
            : 1;

    /// <summary>Every finished building: plot and damage.</summary>
    public static IEnumerable<(string PlotId, int Damage)> Buildings(Village village) =>
        village.Structures.Select(s => (s.PlotId, s.Damage)).Concat(village.Storages.Select(s => (s.PlotId, s.Damage)))
            .Concat(village.Fortifications.Select(f => (f.Id, f.Damage)))
            .Concat(village.Sites.Where(s=>!village.Structures.Any(b=>b.PlotId==s.PlotId)&&!village.Storages.Any(b=>b.PlotId==s.PlotId)).Select(s=>(s.PlotId,s.Damage)))
            .Concat(village.BridgeOpened||village.BridgeRuinedAt is not null?new[]{(RuinRules.RoyalBridge,village.BridgeDamage)}:[]);

    /// <summary>The building most in need of repair, or null when none is damaged.</summary>
    public static string? MostDamaged(Village village, Guid? actor = null) =>
        Buildings(village).Where(b => b.Damage > 0 && (actor is null || OwnerOf(village,b.PlotId) is not {} owner || owner == actor)).OrderByDescending(b => b.Damage).Select(b => b.PlotId).FirstOrDefault();

    private static Guid? OwnerOf(Village village,string plot) => village.Structures.FirstOrDefault(s=>s.PlotId==plot)?.OwnerAccountId
        ?? village.Storages.FirstOrDefault(s=>s.PlotId==plot)?.OwnerAccountId ?? village.Sites.FirstOrDefault(s=>s.PlotId==plot)?.OwnerAccountId;

    public static TilePoint RepairYard(Village village,string plot)
    {
        if(plot==RuinRules.RoyalBridge)return new RaidNavigation(village).NearestFree(RuinRules.TargetYard(plot));
        var wall=village.Fortifications.FirstOrDefault(f=>f.Id==plot);
        return wall is null?WorldMap.Yard(plot):new TilePoint(wall.X+.5+(wall.X==-1?1:0),wall.Y+.5+(wall.Y==-1?1:wall.Y==26?-1:0));
    }

    /// <summary>Adds damage to a building, up to <see cref="MaximumDamage"/>. Returns the damage done.</summary>
    public static int Damage(Village village, string plotId, int amount)
    {
        var current = DamageOf(village, plotId);
        var added = Math.Clamp(amount, 0, MaximumDamage - current);
        SetDamage(village, plotId, current + added);
        return added;
    }

    /// <summary>Mends the most damaged building. Returns the damage repaired.</summary>
    public static int Repair(Village village, int amount, string? targetPlotId = null)
    {
        if ((targetPlotId ?? MostDamaged(village)) is not { } plotId)
        {
            return 0;
        }

        var current = DamageOf(village, plotId);
        var repaired = Math.Clamp(amount, 0, current);
        SetDamage(village, plotId, current - repaired);
        RuinRules.Restored(village,plotId);
        return repaired;
    }

    public static int DamageOf(Village village, string plotId) => plotId==RuinRules.RoyalBridge?village.BridgeDamage:
        village.Structures.FirstOrDefault(s => s.PlotId == plotId)?.Damage
        ?? village.Storages.FirstOrDefault(s => s.PlotId == plotId)?.Damage
        ?? village.Fortifications.FirstOrDefault(f => f.Id == plotId)?.Damage
        ?? village.Sites.FirstOrDefault(s=>s.PlotId==plotId)?.Damage ?? 0;

    private static void SetDamage(Village village, string plotId, int damage)
    {
        if(plotId==RuinRules.RoyalBridge){village.BridgeDamage=damage;return;}
        if (village.Fortifications.FirstOrDefault(f => f.Id == plotId) is {} wall) { wall.Damage = damage; return; }
        if (village.Structures.FirstOrDefault(s => s.PlotId == plotId) is { } structure)
        {
            structure.Damage = damage;
        }
        else if (village.Storages.FirstOrDefault(s => s.PlotId == plotId) is { } storage)
        {
            storage.Damage = damage;
        }
        else if(village.Sites.FirstOrDefault(s=>s.PlotId==plotId) is {} site)site.Damage=damage;
        if(village.Sites.FirstOrDefault(s=>s.PlotId==plotId&&s.RuinedAt!=null) is {} rebuilding)rebuilding.Damage=damage;
    }

    private static int Available(Citizen citizen, Village village, ResourceKind kind) => citizen.Carried(kind) + village.Stock(kind);

    private static void TakeOne(Citizen citizen, Village village, ResourceKind kind) => Take(citizen, village, kind, 1);

    private static void Take(Citizen citizen, Village village, ResourceKind kind, int amount, int? sharpen = null)
    {
        var fromHands = sharpen is int level ? citizen.TakeFromLoad(kind, amount, level) : citizen.TakeFromLoad(kind, amount);
        village.Take(kind, amount - fromHands, sharpen);
    }

    /// <summary>Fills the quiver from one arrow edge. A quiver that already holds another edge is left as it is.</summary>
    private static void FillQuiver(Citizen citizen, Village village, int edge)
    {
        if (citizen.Arrows > 0 && citizen.ArrowSharpen != edge)
        {
            return;
        }

        var room = ArmoryCatalog.QuiverSize - citizen.Arrows;
        var loose = WeaponSharpening.Count(citizen, village, edge) - (citizen.ArrowSharpen == edge ? citizen.Arrows : 0);
        var take = Math.Min(room, loose);
        if (take <= 0)
        {
            return;
        }

        Take(citizen, village, ResourceKind.Arrows, take, edge);
        citizen.Arrows += take;
        citizen.ArrowSharpen = edge;
    }

    /// <summary>The sharpest usable weapon the citizen can reach. Equal power keeps the catalog order: sword, then bow, then spear.</summary>
    private readonly record struct WeaponPick(WeaponDefinition Weapon, int Sharpen, int ArrowEdge);

    private static WeaponPick? BestWeapon(Citizen citizen, Village village)
    {
        WeaponPick? best = null;
        var rank = 0;
        var bestRank = int.MaxValue;
        foreach (var weapon in ArmoryCatalog.Weapons)
        {
            if (weapon.Kind == ResourceKind.Bow)
            {
                if (Available(citizen, village, ResourceKind.Bow) > 0
                    && WeaponSharpening.BestEdge(citizen, village, weapon.Arrows) is int edge)
                {
                    Consider(weapon, 0, edge);
                }

                rank++;
                continue;
            }

            foreach (var sharpen in WeaponSharpening.Levels(citizen, village, weapon.Kind).Distinct())
            {
                Consider(weapon, sharpen, 0);
            }

            rank++;
        }

        return best;

        void Consider(WeaponDefinition weapon, int sharpen, int arrowEdge)
        {
            var power = weapon.Power + WeaponSharpening.Bonus(weapon.Kind == ResourceKind.Bow ? arrowEdge : sharpen);
            var bestPower = best is null
                ? double.MinValue
                : best.Value.Weapon.Power + WeaponSharpening.Bonus(best.Value.Weapon.Kind == ResourceKind.Bow ? best.Value.ArrowEdge : best.Value.Sharpen);
            if (best is null || power > bestPower || power == bestPower && rank < bestRank)
            {
                best = new WeaponPick(weapon, sharpen, arrowEdge);
                bestRank = rank;
            }
        }
    }
}
