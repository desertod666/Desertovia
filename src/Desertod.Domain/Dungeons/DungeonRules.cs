using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.World;

namespace Desertod.Domain.Dungeons;

/// <summary>What one hero did on the dungeon map, handed over when they leave it.</summary>
/// <param name="FoesHit">Foes the hero struck; each blow-trading foe may break their gear.</param>
/// <param name="ArrowsLeft">An archer's arrows still in the quiver.</param>
/// <param name="Healed">A healer who healed anyone used up their remedy.</param>
/// <param name="KnockedOut">Fell in the fight: wounded by <paramref name="Killer"/>.</param>
public sealed record DungeonHeroOutcome(
    int Kills,
    int Experience,
    IReadOnlyList<ResourceAmount> Loot,
    int FoesHit,
    int ArrowsLeft,
    bool Healed,
    bool KnockedOut,
    EnemyKind? Killer);

/// <summary>
/// The Tsar calls the tribe into a dungeon. Citizens answer during the gathering and pick a class by the gear in storage
/// and their crafts; the gear is taken from storage at once. When the gathering closes, the heroes walk to the edge of
/// the land and the fight starts. Each hero comes back on their own, with what they won and, if they fell, a wound.
/// </summary>
public static class DungeonRules
{
    public static TimeSpan Gathering { get; } = TimeSpan.FromSeconds(60);

    /// <summary>The fight starts this long after the gathering at the latest, even if someone is still on the way.</summary>
    public static TimeSpan LongestMarch { get; } = TimeSpan.FromSeconds(45);

    /// <summary>An ended run stays in the village state this long, so everyone sees how it went.</summary>
    public static TimeSpan ShownAfterEnd { get; } = TimeSpan.FromMinutes(2);

    /// <summary>The heroes gather at the west edge of the land, on the camp's row, and come back from there.</summary>
    public static TilePoint MarchSpot { get; } = new(WorldMap.Land.X + 0.5, WorldMap.Places[WorldMap.Camp].Y);

    public static DungeonRun Open(Village village, string dungeonId, string tsarName, int seed, DateTimeOffset now)
    {
        SafeRest.RequireActive(village.TsarRestState);
        var dungeon = DungeonCatalog.Get(dungeonId);
        if(RegionalTravel.WesternExit(village) is null)throw new GameRuleException("spot.noPath","There is no open route to the dungeon departure.");
        return new DungeonRun
        {
            VillageId = village.Id,
            DungeonId = dungeon.Id,
            Phase = DungeonPhase.Gathering,
            Outcome = DungeonOutcome.Pending,
            OpenedAt = now,
            GatheringEndsAt = now + Gathering,
            Seed = seed,
            Foes = dungeon.TotalFoes,
            Heroes = [new DungeonHero { AccountId = village.OwnerAccountId, Name = tsarName, Class = HeroClass.Voivode }],
        };
    }

    /// <summary>Classes the citizen may pick now; the gear they already hold for <paramref name="hero"/> counts as available.</summary>
    public static IReadOnlyList<HeroClass> AvailableClasses(Citizen citizen, Village village, DungeonHero? hero = null) =>
        HeroClassCatalog.Classes
            .Where(c => c.Class != HeroClass.Voivode && IsAvailable(citizen, village, c, hero))
            .Select(c => c.Class)
            .ToList();

    /// <summary>Answers the gathering, or switches class: the gear of the old class goes back first.</summary>
    public static DungeonHero Join(DungeonRun run, Village village, Citizen citizen, HeroClass heroClass, DateTimeOffset now)
    {
        SafeRest.RequireActive(citizen.RestState);
        RequireGathering(run, now);
        if (citizen.Status != CitizenStatus.Active)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        HospitalRules.RequireReady(citizen, now);
        if (CombatRules.IsInjured(citizen, now))
        {
            throw new GameRuleException("dungeon.injured", "You are wounded; a remedy heals it at once.",
                new Dictionary<string, object> { ["readyAt"] = citizen.InjuredUntil!.Value });
        }

        if (citizen.DungeonRunId is { } other && other != run.Id)
        {
            throw new GameRuleException("dungeon.busy", "You are away in the dungeon.");
        }

        var definition = HeroClassCatalog.Get(heroClass);
        var hero = run.Hero(citizen.Id);
        if (heroClass == HeroClass.Voivode || !IsAvailable(citizen, village, definition, hero))
        {
            throw new GameRuleException("dungeon.classUnavailable", "This class needs gear or a craft you do not have.",
                new Dictionary<string, object> { ["class"] = heroClass.ToString() });
        }

        if (citizen.WorkState(now) == WorkState.Working)
        {
            CycleEngine.Cancel(citizen, now, village);
        }

        if (hero is null)
        {
            hero = new DungeonHero { CitizenId = citizen.Id, AccountId = citizen.AccountId, Name = citizen.Name };
            run.Heroes.Add(hero);
        }

        ReturnGear(citizen, village, hero);
        hero.Class = heroClass;
        TakeGear(citizen, village, definition, hero);
        citizen.DungeonRunId = run.Id;
        return hero;
    }

    /// <summary>Takes back the answer while the gathering lasts.</summary>
    public static void Leave(DungeonRun run, Village village, Citizen citizen, DateTimeOffset now)
    {
        RequireGathering(run, now);
        var hero = run.Hero(citizen.Id) ?? throw new GameRuleException("dungeon.notJoined", "You have not answered the call.");
        ReturnGear(citizen, village, hero);
        run.Heroes.Remove(hero);
        citizen.DungeonRunId = null;
    }

    /// <summary>
    /// The citizen leaves the village: their gear goes back to storage. Before the march they drop out of the run;
    /// later their hero stays in it, and what they find is lost.
    /// </summary>
    public static void Desert(DungeonRun run, Village village, Citizen citizen)
    {
        if (run.Hero(citizen.Id) is not { } hero)
        {
            citizen.DungeonRunId = null;
            return;
        }

        ReturnGear(citizen, village, hero);
        if (run.Phase == DungeonPhase.Gathering)
        {
            run.Heroes.Remove(hero);
        }

        citizen.DungeonRunId = null;
    }

    /// <summary>The Tsar calls it off before the march: everyone puts their gear back.</summary>
    public static void Cancel(DungeonRun run, Village village, IEnumerable<Citizen> citizens, DateTimeOffset now)
    {
        if (run.Phase != DungeonPhase.Gathering)
        {
            throw new GameRuleException("dungeon.notGathering", "The heroes have already set out.");
        }

        foreach (var citizen in citizens.Where(c => run.Hero(c.Id) is not null))
        {
            ReturnGear(citizen, village, run.Hero(citizen.Id)!);
            citizen.DungeonRunId = null;
        }

        run.Phase = DungeonPhase.Ended;
        run.Outcome = DungeonOutcome.Cancelled;
        run.EndedAt = now;
    }

    /// <summary>
    /// The gathering is over: heroes who can still walk go to the edge of the land. Someone already down stays where
    /// they fell, with their gear put back, and does not enter the dungeon.
    /// </summary>
    public static void March(DungeonRun run, Village village, IEnumerable<Citizen> citizens, DateTimeOffset now,IReadOnlySet<Guid>? busyAccounts=null)
    {
        var latest = now;
        var anyone = false;
        if(RegionalTravel.WesternExit(village) is not {} destination)
        {
            Cancel(run,village,citizens,now);
            return;
        }
        foreach (var citizen in citizens)
        {
            if (run.Hero(citizen.Id) is not { } hero)
            {
                continue;
            }

            if (!CanMarch(citizen, now)||busyAccounts?.Contains(citizen.AccountId)==true)
            {
                ReturnGear(citizen, village, hero);
                run.Heroes.Remove(hero);
                citizen.DungeonRunId = null;
                continue;
            }

            try{citizen.SetOut(destination,village,now);}
            catch(GameRuleException e) when(e.Code is "spot.noPath" or "bridge.closed")
            {
                ReturnGear(citizen,village,hero);run.Heroes.Remove(hero);citizen.DungeonRunId=null;
                continue;
            }
            citizen.IdleSpot = destination;
            hero.ArrivesAt = citizen.Trip!.ArrivesAt;
            latest = Max(latest, hero.ArrivesAt.Value);
            anyone = true;
        }

        if (TsarIsDown(village, now)||busyAccounts?.Contains(village.OwnerAccountId)==true)
        {
            run.Tsar.Settled = true;
        }
        else
        {
            try
            {
                village.TsarGoTo(destination,now);
                run.Tsar.ArrivesAt=village.TsarTrip!.ArrivesAt;
                latest=Max(latest,run.Tsar.ArrivesAt.Value);anyone=true;
            }
            catch(GameRuleException e) when(e.Code is "spot.noPath" or "bridge.closed"){run.Tsar.Settled=true;}
        }

        if (!anyone)
        {
            run.Tsar.Settled = true;
            Finish(run, DungeonOutcome.Abandoned, 0, now);
            return;
        }

        run.Phase = DungeonPhase.Marching;
        run.MarchEndsAt = latest < now + LongestMarch ? latest : now + LongestMarch;
    }

    /// <summary>A citizen who is wounded, in care, exhausted or dormant does not set out.</summary>
    public static bool CanMarch(Citizen citizen, DateTimeOffset now) =>
        citizen.Status == CitizenStatus.Active
        && !CombatRules.IsInjured(citizen, now)
        && !HospitalRules.IsAdmitted(citizen, now)
        && citizen.RecoveryPausedAt is null
        && !(citizen.ExhaustedUntil > now);

    /// <summary>Only heroes who really reached the departure may enter the scene after the bounded march.</summary>
    public static bool CompleteMarch(DungeonRun run,Village village,IEnumerable<Citizen> citizens,DateTimeOffset now)
    {
        static bool Arrived(Trip? trip,DateTimeOffset at)=>trip is null||!trip.Waiting(at)&&trip.ArrivesAt<=at;
        var present=new HashSet<Guid>();
        foreach(var citizen in citizens)
        {
            present.Add(citizen.Id);
            if(run.Hero(citizen.Id) is not {Settled:false} hero)continue;
            var ready=CanMarch(citizen,now);
            if(ready&&Arrived(citizen.Trip,now))continue;
            ReturnGear(citizen,village,hero);run.Heroes.Remove(hero);citizen.DungeonRunId=null;
            if(ready){citizen.SetOut(WorldMap.Places[WorldMap.Camp],village,now,waitIfBlocked:true);citizen.IdleSpot=null;}
            else
            {
                citizen.IdleSpot=citizen.Trip?.PositionAt(now)??citizen.IdleSpot;citizen.Spot=citizen.IdleSpot;citizen.Trip=null;
            }
        }
        foreach(var missing in run.Heroes.Where(h=>!h.IsTsar&&h.CitizenId is {} id&&!present.Contains(id)))missing.Settled=true;
        if(!run.Tsar.Settled&&(TsarIsDown(village,now)||!Arrived(village.TsarTrip,now)))
        {
            run.Tsar.Settled=true;
            if(!TsarIsDown(village,now))village.TsarGoTo(WorldMap.Places[WorldMap.Camp],now,waitIfBlocked:true);
            else{village.TsarSpot=village.TsarTrip?.PositionAt(now)??village.TsarSpot;village.TsarTrip=null;}
        }
        if(run.Heroes.Any(h=>!h.Settled))return true;
        Finish(run,DungeonOutcome.Abandoned,0,now);return false;
    }

    private static bool TsarIsDown(Village village, DateTimeOffset now) =>
        village.TsarInjuredUntil > now || village.TsarHospitalizedAt is not null;

    public static void Start(DungeonRun run, DateTimeOffset now)
    {
        if (run.Phase != DungeonPhase.Marching)
        {
            throw new InvalidOperationException("Only a marching run can start.");
        }

        run.Phase = DungeonPhase.Running;
        run.StartedAt = now;
    }

    /// <summary>
    /// A hero is out of the dungeon. A citizen stores the goods (what does not fit stays in their hands), gets the
    /// experience, and may find their gear broken. Someone who fell stays wounded where they are. Anyone still
    /// standing walks home.
    /// </summary>
    public static void Settle(DungeonRun run, DungeonHero hero, Citizen? citizen, Village village, DungeonHeroOutcome outcome, Random random, DateTimeOffset now)
    {
        if (hero.Settled)
        {
            return;
        }

        hero.Settled = true;
        hero.Kills = outcome.Kills;
        hero.KnockedOut = outcome.KnockedOut;
        hero.Killer = outcome.Killer;
        if (hero.IsTsar)
        {
            if (outcome.KnockedOut)
            {
                StayDown(village, outcome, now);
            }
            else
            {
                village.TsarGoTo(WorldMap.Places[WorldMap.Camp], now, waitIfBlocked:true);
            }

            return;
        }

        if (citizen is null)
        {
            return;
        }

        var definition = HeroClassCatalog.Get(hero.Class);
        hero.Experience = outcome.Experience;
        var renown = Progression.AddExperience(citizen.Stats, definition.Stat, outcome.Experience).RenownGained;
        SkillRules.AddExperience(citizen, Skill.Guarding, outcome.Experience);
        SkillRules.CatchUp(citizen);
        citizen.Renown += renown;

        foreach (var good in outcome.Loot.Where(g => g.Amount > 0))
        {
            var stored = village.Store(good.Resource, good.Amount);
            if (stored < good.Amount)
            {
                citizen.Carry(good.Resource, good.Amount - stored);
            }

            hero.Loot.Add(new DungeonGood { Kind = good.Resource, Amount = good.Amount });
        }

        if (citizen.Weapon == ResourceKind.Bow)
        {
            citizen.Arrows = Math.Clamp(outcome.ArrowsLeft, 0, citizen.Arrows);
            if (citizen.Arrows == 0)
            {
                citizen.ArrowSharpen = 0;
            }
        }

        if (outcome.Healed && hero.Remedies > 0)
        {
            hero.Remedies--;
        }

        Wear(citizen, outcome.FoesHit, random, hero.Broken);
        ReturnGear(citizen, village, hero);
        citizen.DungeonRunId = null;
        if (outcome.KnockedOut)
        {
            if (outcome.Killer is { } killer)
            {
                CombatRules.Injure(citizen, TimeSpan.FromMinutes(EnemyCatalog.Get(killer).InjuryMinutes), now, rewardEligible: true);
            }

            StayDown(citizen, now);
        }
        else
        {
            citizen.SetOut(WorldMap.Places[WorldMap.Camp], village, now, waitIfBlocked:true);
            citizen.IdleSpot = null;
        }
    }

    /// <summary>A fallen hero is down. The walk home is not started, so they are not both wounded and on the road.</summary>
    private static void StayDown(Citizen citizen, DateTimeOffset now)
    {
        if (citizen.Trip is { } trip)
        {
            var here = trip.PositionAt(now);
            citizen.IdleSpot = here;
            citizen.Spot = here;
        }

        citizen.Trip = null;
    }

    private static void StayDown(Village village, DungeonHeroOutcome outcome, DateTimeOffset now)
    {
        if (outcome.Killer is { } killer)
        {
            var until = now.AddMinutes(EnemyCatalog.Get(killer).InjuryMinutes);
            if (village.TsarInjuredUntil is not { } current || current < until)
            {
                village.TsarInjuredUntil = until;
                village.TsarWoundedAt = now;
                village.TsarHospitalizedAt = null;
                village.TsarRescueByCitizenId = null;
                village.TsarRescueRewardEligible = true;
                village.TsarRescueRequestedAt = null;
            }
        }

        if (village.TsarTrip is { } trip)
        {
            village.TsarSpot = trip.PositionAt(now);
        }

        village.TsarTrip = null;
    }

    public static void Finish(DungeonRun run, DungeonOutcome outcome, int slain, DateTimeOffset now)
    {
        run.Phase = DungeonPhase.Ended;
        run.Outcome = outcome;
        run.Slain = slain;
        run.EndedAt = now;
    }

    /// <summary>
    /// The server stopped mid-run: the fight is lost for the record. Citizens put their gear back and walk home unhurt;
    /// what they won so far was already stored when each of them left.
    /// </summary>
    public static void Abandon(DungeonRun run, Village village, IEnumerable<Citizen> citizens, DateTimeOffset now)
    {
        foreach (var citizen in citizens)
        {
            if (run.Hero(citizen.Id) is not { Settled: false } hero)
            {
                continue;
            }

            ReturnGear(citizen, village, hero);
            citizen.DungeonRunId = null;
            if (run.Phase != DungeonPhase.Gathering)
            {
                citizen.SetOut(WorldMap.Places[WorldMap.Camp], village, now, waitIfBlocked:true);
                citizen.IdleSpot = null;
            }

            hero.Settled = true;
        }

        if (run.Phase != DungeonPhase.Gathering)
        {
            village.TsarGoTo(WorldMap.Places[WorldMap.Camp], now, waitIfBlocked:true);
        }

        run.Tsar.Settled = true;
        Finish(run, DungeonOutcome.Abandoned, run.Slain, now);
    }

    /// <summary>Health of a citizen hero of this class.</summary>
    public static double Health(Citizen citizen, HeroClassDefinition definition) =>
        HeroClassCatalog.Health(citizen.Stats.Endurance, definition);

    /// <summary>Damage of one blow: the fight power of <see cref="CombatRules.Power"/> with the class weapon.</summary>
    public static double Damage(Citizen citizen, HeroClassDefinition definition) =>
        (CombatRules.UnarmedPower
         + CombatRules.StrengthPower * citizen.Stats.Strength
         + CombatRules.DexterityPower * citizen.Stats.Dexterity
         + CombatRules.GuardingPower * citizen.SkillLevel(Skill.Guarding)
         + (ArmoryCatalog.Weapon(definition.Weapon)?.Power ?? 0)
         + WeaponSharpening.Bonus(definition.Weapon == ResourceKind.Bow
             ? citizen.ArrowSharpen
             : citizen.Weapon == definition.Weapon ? citizen.WeaponSharpen : 0))
        * definition.DamageMultiplier;

    /// <summary>Share of each blow the armor stops.</summary>
    public static double Protection(Citizen citizen) => ArmoryCatalog.Armor(citizen.Armor)?.Protection ?? 0;

    private static void RequireGathering(DungeonRun run, DateTimeOffset now)
    {
        if (run.Phase != DungeonPhase.Gathering || now >= run.GatheringEndsAt)
        {
            throw new GameRuleException("dungeon.notGathering", "The heroes have already set out.");
        }
    }

    private static bool IsAvailable(Citizen citizen, Village village, HeroClassDefinition definition, DungeonHero? hero)
    {
        if (definition.Skill is { } skill && citizen.SkillLevel(skill) < definition.SkillLevel)
        {
            return false;
        }

        if (definition.Weapon is { } weapon && Available(citizen, village, weapon) == 0)
        {
            return false;
        }

        if (definition.Armor is { } armor && Available(citizen, village, armor) == 0)
        {
            return false;
        }

        if (definition.Class == HeroClass.Archer && Available(citizen, village, ResourceKind.Arrows) == 0)
        {
            return false;
        }

        return definition.Heal <= 0 || Available(citizen, village, ResourceKind.Remedy) + (hero?.Remedies ?? 0) > 0;
    }

    /// <summary>In storage, in the citizen's hands, or already held as gear.</summary>
    private static int Available(Citizen citizen, Village village, ResourceKind kind) =>
        village.Stock(kind)
        + citizen.Carried(kind)
        + (citizen.Weapon == kind || citizen.Armor == kind ? 1 : 0)
        + (kind == ResourceKind.Arrows ? citizen.Arrows : 0);

    private static void TakeGear(Citizen citizen, Village village, HeroClassDefinition definition, DungeonHero hero)
    {
        if (definition.Weapon is { } weapon)
        {
            Hold(citizen, village, weapon, isWeapon: true);
        }

        var armor = definition.Armor
                    ?? (Available(citizen, village, ResourceKind.LeatherArmor) > 0 ? ResourceKind.LeatherArmor : (ResourceKind?)null);
        if (armor is { } kind)
        {
            Hold(citizen, village, kind, isWeapon: false);
        }

        if (definition.Class == HeroClass.Archer)
        {
            var edge = citizen.Arrows > 0
                ? citizen.ArrowSharpen
                : WeaponSharpening.BestEdge(citizen, village, 1);
            if (edge is int level)
            {
                var wanted = Math.Max(0, HeroClassCatalog.Quiver - citizen.Arrows);
                var taken = Take(citizen, village, ResourceKind.Arrows, wanted, level);
                if (taken > 0 && citizen.Arrows == 0)
                {
                    citizen.ArrowSharpen = level;
                }

                citizen.Arrows += taken;
            }

            hero.Arrows = citizen.Arrows;
        }

        if (definition.Heal > 0 && hero.Remedies < HeroClassCatalog.HealerRemedies)
        {
            hero.Remedies += Take(citizen, village, ResourceKind.Remedy, HeroClassCatalog.HealerRemedies - hero.Remedies);
        }
    }

    /// <summary>Gear and the healer's remedy go back to storage; what does not fit stays with the citizen.</summary>
    private static void ReturnGear(Citizen citizen, Village village, DungeonHero hero)
    {
        CombatRules.Disarm(citizen, village);
        if (hero.Remedies > 0)
        {
            var stored = village.Store(ResourceKind.Remedy, hero.Remedies);
            if (stored < hero.Remedies)
            {
                citizen.Carry(ResourceKind.Remedy, hero.Remedies - stored);
            }
        }

        hero.Remedies = 0;
        hero.Arrows = 0;
    }

    /// <summary>Puts one piece of gear in the citizen's hand; another one still held (storage was full) goes to their load.</summary>
    private static void Hold(Citizen citizen, Village village, ResourceKind kind, bool isWeapon)
    {
        var held = isWeapon ? citizen.Weapon : citizen.Armor;
        if (held == kind)
        {
            return;
        }

        if (held is { } other)
        {
            citizen.Carry(other, 1, isWeapon ? citizen.WeaponSharpen : 0);
            if (isWeapon)
            {
                citizen.ClearWeapon();
            }
            else
            {
                citizen.Armor = null;
            }
        }

        var sharpen = 0;
        if (isWeapon)
        {
            var levels = WeaponSharpening.Levels(citizen, village, kind).ToList();
            if (levels.Count == 0)
            {
                citizen.ClearWeapon();
                return;
            }

            sharpen = levels.Max();
        }

        if (Take(citizen, village, kind, 1, isWeapon ? sharpen : null) == 0)
        {
            if (isWeapon)
            {
                citizen.ClearWeapon();
            }
            else
            {
                citizen.Armor = null;
            }

            return;
        }

        if (isWeapon)
        {
            citizen.Weapon = kind;
            citizen.WeaponSharpen = sharpen;
        }
        else
        {
            citizen.Armor = kind;
        }
    }

    private static int Take(Citizen citizen, Village village, ResourceKind kind, int amount, int? sharpen = null)
    {
        var fromHands = sharpen is int level ? citizen.TakeFromLoad(kind, amount, level) : citizen.TakeFromLoad(kind, amount);
        return fromHands + village.Take(kind, amount - fromHands, sharpen);
    }

    /// <summary>Each foe struck may break the weapon and the armor, the same chances as a fight in the wild.</summary>
    private static void Wear(Citizen citizen, int foesHit, Random random, List<ResourceKind> broken)
    {
        for (var i = 0; i < foesHit; i++)
        {
            if (ArmoryCatalog.Weapon(citizen.Weapon) is { } weapon && random.NextDouble() < weapon.BreakChance)
            {
                broken.Add(weapon.Kind);
                citizen.ClearWeapon();
            }

            if (ArmoryCatalog.Armor(citizen.Armor) is { } armor && random.NextDouble() < armor.BreakChance)
            {
                broken.Add(armor.Kind);
                citizen.Armor = null;
            }
        }
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
