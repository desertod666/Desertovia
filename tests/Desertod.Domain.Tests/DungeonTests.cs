using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class DungeonTests
{
    /// <summary>Every roll succeeds and every amount is the smallest.</summary>
    private sealed class LuckyRandom : Random
    {
        public override double NextDouble() => 0;

        public override int Next(int minValue, int maxValue) => minValue;
    }

    private static DungeonDefinition Corridor(string middle, double timeLimitSeconds = 900) =>
        DungeonCatalog.Parse("test", "Test", ["TTTTTTTTTT", "E" + middle + "T", "TTTTTTTTTT"], TimeSpan.FromSeconds(timeLimitSeconds));

    private static DungeonFighter Fighter(HeroClass heroClass = HeroClass.Brawler, double health = 100, double damage = 1, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), "Hero", heroClass, health, damage, 0, null, false);

    private static void Run(DungeonSimulation simulation, double seconds, Random? random = null)
    {
        random ??= new Random(1);
        for (var elapsed = 0.0; elapsed < seconds - 1e-9 && !simulation.IsOver; elapsed += 0.1)
        {
            simulation.Tick(0.1, random);
        }
    }

    [Fact]
    public void DeadLands_EveryFoeCanBeReachedFromTheEntrance()
    {
        var dungeon = DungeonCatalog.Get(DungeonCatalog.DeadLands);

        Assert.Equal(3, dungeon.Floors.Count);
        Assert.Equal(dungeon.Floors.Sum(floor => floor.Spawns.Count), dungeon.TotalFoes);
        Assert.DoesNotContain(dungeon.Floors[0].Spawns, spawn => spawn.Kind == EnemyKind.BarrowKing);
        Assert.DoesNotContain(dungeon.Floors[1].Spawns, spawn => spawn.Kind == EnemyKind.BarrowKing);
        Assert.Equal([EnemyKind.BarrowKing], dungeon.Floors[2].Spawns.Select(spawn => spawn.Kind));
        Assert.All(dungeon.Floors, floor =>
        {
            var entrance = ((int)floor.Entrance.X, (int)floor.Entrance.Y);
            Assert.All(floor.Spawns, spawn => Assert.NotNull(DungeonPathfinder.Find(floor, entrance, (spawn.X, spawn.Y))));
        });
    }

    [Fact]
    public void AFallenHero_LivesAndStaysBehind_WhileTheSurvivorFacesTheBoss()
    {
        var woods = DungeonCatalog.Parse("woods", "Woods", ["TTTTTTTTTT", "Er.......T", "TTTTTTTTTT"], TimeSpan.FromMinutes(15));
        var barrow = DungeonCatalog.Parse("barrow", "Barrow", ["TTTTTTTTTT", "E.......KT", "TTTTTTTTTT"], TimeSpan.FromMinutes(15));
        var course = DungeonCatalog.Expedition("run", "Run", TimeSpan.FromMinutes(15), woods, barrow);
        var fallen = Fighter(health: 30, damage: 1);
        var survivor = Fighter(health: 200, damage: 1000);
        var simulation = new DungeonSimulation(course, [fallen, survivor]);
        simulation.Hero(fallen.Id)!.State = HeroState.Fleeing;
        simulation.Hero(fallen.Id)!.Health = 0;
        simulation.Attack(survivor.Id, 0);

        Run(simulation, 2);

        Assert.Equal(1, simulation.Floor);
        Assert.Equal(DungeonOutcome.Pending, simulation.Outcome);
        Assert.Equal(HeroState.Left, simulation.Hero(fallen.Id)!.State);
        Assert.Equal(HeroState.Fighting, simulation.Hero(survivor.Id)!.State);
        Assert.NotEqual(simulation.Hero(survivor.Id)!.X, simulation.Hero(fallen.Id)!.X);
        Assert.Equal(200, simulation.Hero(survivor.Id)!.Health);
        Assert.Equal(EnemyKind.BarrowKing, Assert.Single(simulation.Foes, foe => foe.IsAlive).Kind);

        simulation.Attack(survivor.Id, 0);
        Run(simulation, 20);

        Assert.Equal(DungeonOutcome.Victory, simulation.Outcome);
        Assert.True(simulation.OutcomeOf(simulation.Hero(fallen.Id)!).KnockedOut);
        Assert.False(simulation.OutcomeOf(simulation.Hero(survivor.Id)!).KnockedOut);
    }

    [Fact]
    public void DungeonFoes_NeverAnswerTheTsarsCall()
    {
        Assert.All(DungeonCatalog.Foes.Keys.Where(k => k is not EnemyKind.Zombie and not EnemyKind.Skeleton),
            kind => Assert.True(EnemyCatalog.Get(kind).DungeonOnly));
    }

    [Fact]
    public void AvailableClasses_FollowTheGearInStorageAndTheCrafts()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);

        Assert.Equal(new[] { HeroClass.Brawler }, DungeonRules.AvailableClasses(citizen, village));

        village.Store(ResourceKind.Spear, 1);
        village.Store(ResourceKind.Sword, 1);
        village.Store(ResourceKind.Remedy, 1);
        Assert.Equal(new[] { HeroClass.Brawler, HeroClass.Spearman }, DungeonRules.AvailableClasses(citizen, village));

        citizen.WithSkill(Skill.Guarding, 2).WithSkill(Skill.Scholarship, 2);
        Assert.Equal(new[] { HeroClass.Brawler, HeroClass.Spearman, HeroClass.Swordsman, HeroClass.Healer }, DungeonRules.AvailableClasses(citizen, village));

        village.Store(ResourceKind.Bow, 1);
        citizen.WithSkill(Skill.Hunting, 2);
        Assert.DoesNotContain(HeroClass.Archer, DungeonRules.AvailableClasses(citizen, village));
        village.Store(ResourceKind.Arrows, 3);
        village.Store(ResourceKind.Shield, 1);
        Assert.Contains(HeroClass.Archer, DungeonRules.AvailableClasses(citizen, village));
        Assert.Contains(HeroClass.Shieldbearer, DungeonRules.AvailableClasses(citizen, village));
        Assert.DoesNotContain(HeroClass.Voivode, DungeonRules.AvailableClasses(citizen, village));
    }

    [Fact]
    public void Join_TakesTheGearFromStorage_AndLeaving_PutsItBack()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village).WithSkill(Skill.Guarding, 2);
        village.Store(ResourceKind.Sword, 1);
        village.Store(ResourceKind.LeatherArmor, 2); // One real set is reserved by each ordinary body.
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        DungeonRules.Join(run, village, citizen, HeroClass.Swordsman, Now);

        Assert.Equal(ResourceKind.Sword, citizen.Weapon);
        Assert.Equal(ResourceKind.LeatherArmor, citizen.Armor);
        Assert.Equal(0, village.Stock(ResourceKind.Sword));
        Assert.Equal(run.Id, citizen.DungeonRunId);
        Assert.Equal(2, run.Heroes.Count);

        DungeonRules.Leave(run, village, citizen, Now);

        Assert.Null(citizen.Weapon);
        Assert.Null(citizen.Armor);
        Assert.Equal(1, village.Stock(ResourceKind.Sword));
        Assert.Equal(1, village.Stock(ResourceKind.LeatherArmor));
        Assert.Null(citizen.DungeonRunId);
        Assert.Single(run.Heroes);
    }

    [Fact]
    public void Join_ChangingClass_SwapsTheGear()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        village.Store(ResourceKind.Spear, 1);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        DungeonRules.Join(run, village, citizen, HeroClass.Spearman, Now);
        Assert.Equal(new[] { HeroClass.Brawler, HeroClass.Spearman }, DungeonRules.AvailableClasses(citizen, village, run.Hero(citizen.Id)));
        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);

        Assert.Null(citizen.Weapon);
        Assert.Equal(1, village.Stock(ResourceKind.Spear));
        Assert.Equal(HeroClass.Brawler, run.Hero(citizen.Id)!.Class);
    }

    [Fact]
    public void Join_CancelsTheRunningTask_AndBlocksNewOnes()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var context = new Rules.WorkContext { Village = village, Random = new Random(1) };
        Rules.CycleEngine.Start(citizen, Task("forage"), context, Now);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);

        Assert.Null(citizen.CurrentTaskId);
        var error = Assert.Throws<GameRuleException>(() => Rules.CycleEngine.Start(citizen, Task("forage"), context, Now));
        Assert.Equal("dungeon.busy", error.Code);
    }

    [Fact]
    public void Join_IsClosedOnceTheGatheringEnds()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        var error = Assert.Throws<GameRuleException>(() => DungeonRules.Join(run, village, citizen, HeroClass.Brawler, run.GatheringEndsAt));

        Assert.Equal("dungeon.notGathering", error.Code);
    }

    [Fact]
    public void Heroes_DoNotAttackByThemselves()
    {
        var simulation = new DungeonSimulation(Corridor(".r......"), [Fighter(health: 1000, damage: 50)]);

        Run(simulation, 3);

        Assert.All(simulation.Foes, foe => Assert.Equal(foe.Definition.Health, foe.Health));
        Assert.True(simulation.Heroes[0].Health < 1000);
    }

    [Fact]
    public void Foes_ChaseAHeroInsideTheirAggroRange_AndLeaveOthersAlone()
    {
        var near = new DungeonSimulation(Corridor("....r..."), [Fighter(health: 1000)]);
        var far = new DungeonSimulation(Corridor(".......r"), [Fighter(health: 1000)]);

        Run(near, 3);
        Run(far, 3);

        Assert.True(near.Foes[0].X < 5.5);
        Assert.True(near.Heroes[0].Health < 1000);
        Assert.Equal(8.5, far.Foes[0].X, 3);
        Assert.Equal(1000, far.Heroes[0].Health);
    }

    [Fact]
    public void A_StruckFoe_ChasesTheArcherFromAnyDistance()
    {
        var archer = Fighter(HeroClass.Archer, health: 1000, damage: 1) with { Arrows = 5 };
        var simulation = new DungeonSimulation(Corridor("......r."), [archer]);
        simulation.MoveTo(archer.Id, 2.5, 1.5);
        Run(simulation, 1);
        simulation.Attack(archer.Id, 0);

        Run(simulation, 3);

        Assert.Equal(archer.Id, simulation.Foes[0].Target);
        Assert.True(simulation.Foes[0].X < 7.5);
        Assert.True(simulation.Heroes[0].Arrows < 5);
    }

    [Fact]
    public void A_SlainFoe_RewardsEveryHeroStillFighting_ButNotTheTsar()
    {
        var killer = Fighter(HeroClass.Spearman, damage: 100);
        var friend = Fighter();
        var tsar = Fighter(HeroClass.Voivode);
        var simulation = new DungeonSimulation(Corridor("........"), [killer, friend, tsar]);
        var far = new DungeonSimulation(Corridor(".r......"), [killer]);

        far.Attack(killer.Id, 0);
        Run(far, 2, new LuckyRandom());

        Assert.Equal(DungeonOutcome.Victory, far.Outcome);
        Assert.Equal(1, far.Heroes[0].Kills);
        Assert.Equal(DungeonCatalog.Foe(EnemyKind.Rat).Experience, far.Heroes[0].Experience);
        Assert.Equal(1, far.Heroes[0].Loot[ResourceKind.Food]);

        var shared = new DungeonSimulation(Corridor(".r......"), [killer, friend, tsar]);
        shared.Attack(killer.Id, 0);
        Run(shared, 2, new LuckyRandom());

        Assert.Equal(shared.Heroes[0].Experience, shared.Heroes[1].Experience);
        Assert.NotEmpty(shared.Heroes[1].Loot);
        Assert.Equal(0, shared.Heroes[2].Experience);
        Assert.Empty(shared.Heroes[2].Loot);
        Assert.Equal(DungeonOutcome.Pending, simulation.Outcome);
    }

    [Fact]
    public void A_FallenHero_FleesToTheEntrance_AndIsOut()
    {
        var weak = Fighter(health: 1);
        var strong = Fighter(health: 10_000);
        var simulation = new DungeonSimulation(Corridor(".r......"), [weak, strong]);

        Run(simulation, 3);

        var fallen = simulation.Hero(weak.Id)!;
        Assert.Equal(HeroState.Left, fallen.State);
        Assert.Equal(EnemyKind.Rat, fallen.Killer);
        Assert.True(simulation.OutcomeOf(fallen).KnockedOut);
        Assert.Equal(strong.Id, simulation.Foes[0].Target);
    }

    [Fact]
    public void TheRun_IsLost_WhenEveryHeroIsOut()
    {
        var simulation = new DungeonSimulation(Corridor(".r......"), [Fighter(health: 1)]);

        Run(simulation, 3);

        Assert.Equal(DungeonOutcome.Defeat, simulation.Outcome);
        Assert.False(simulation.TimedOut);
    }

    [Fact]
    public void An_InvulnerableHero_KeepsFullHealth_AndTheClockDoesNotEndTheRun()
    {
        var hero = Fighter(health: 1, damage: 0) with { Invulnerable = true };
        var simulation = new DungeonSimulation(Corridor(".r......", timeLimitSeconds: 1), [hero]);

        Run(simulation, 3);

        Assert.Equal(DungeonOutcome.Pending, simulation.Outcome);
        Assert.False(simulation.TimedOut);
        Assert.Equal(HeroState.Fighting, simulation.Heroes[0].State);
        Assert.Equal(1, simulation.Heroes[0].Health);
    }

    [Fact]
    public void TheRun_IsLost_WhenTheTimeRunsOut_WithNoWounds()
    {
        var simulation = new DungeonSimulation(Corridor(".......r", timeLimitSeconds: 1), [Fighter()]);

        Run(simulation, 2);

        Assert.Equal(DungeonOutcome.Defeat, simulation.Outcome);
        Assert.True(simulation.TimedOut);
        Assert.False(simulation.OutcomeOf(simulation.Heroes[0]).KnockedOut);
    }

    [Fact]
    public void A_Healer_TendsAHurtAlly()
    {
        var healer = Fighter(HeroClass.Healer) with { CanHeal = true };
        var hurt = Fighter(health: 50);
        var simulation = new DungeonSimulation(Corridor("........"), [healer, hurt]);
        simulation.Hero(hurt.Id)!.Health = 10;

        simulation.Heal(healer.Id, hurt.Id);
        Run(simulation, 0.2);

        Assert.Equal(10 + HeroClassCatalog.Get(HeroClass.Healer).Heal, simulation.Hero(hurt.Id)!.Health);
        Assert.True(simulation.Hero(healer.Id)!.Healed);
    }

    [Fact]
    public void Settle_StoresTheLoot_AndLeavesTheFallenWhereTheyFell()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village, (Stat.Strength, 2));
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);
        DungeonRules.March(run, village, [citizen], Now);
        var iron = village.Stock(ResourceKind.IronOre);
        var back = citizen.Trip!.ArrivesAt + TimeSpan.FromMinutes(1);
        var fallen = citizen.Trip.PositionAt(back);
        var outcome = new DungeonHeroOutcome(1, 20, [new ResourceAmount(ResourceKind.IronOre, 2)], 1, 0, false, true, EnemyKind.Ghoul);

        DungeonRules.Settle(run, run.Hero(citizen.Id)!, citizen, village, outcome, new Random(1), back);

        Assert.Equal(iron + 2, village.Stock(ResourceKind.IronOre));
        Assert.Equal(back + TimeSpan.FromMinutes(EnemyCatalog.Get(EnemyKind.Ghoul).InjuryMinutes), citizen.InjuredUntil);
        Assert.Null(citizen.DungeonRunId);
        Assert.Null(citizen.Trip);
        Assert.Equal(fallen, citizen.IdleSpot);
        Assert.Equal(fallen, citizen.Spot);
        Assert.Equal("citizen.injured", Assert.Throws<GameRuleException>(() => citizen.GoTo(WorldMap.Places[WorldMap.Camp], village, back)).Code);
        Assert.Equal(fallen, citizen.IdleSpot);
        Assert.True(citizen.SkillLevel(Skill.Guarding) > 0 || citizen.Skills.Any(s => s.Kind == Skill.Guarding && s.Experience > 0));
        Assert.True(run.Hero(citizen.Id)!.Settled);
    }

    [Fact]
    public void Settle_SendsALivingHeroHome()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);
        DungeonRules.March(run, village, [citizen], Now);
        var outcome = new DungeonHeroOutcome(1, 10, [], 1, 0, false, false, null);

        DungeonRules.Settle(run, run.Hero(citizen.Id)!, citizen, village, outcome, new Random(1), Now);

        Assert.Null(citizen.InjuredUntil);
        Assert.Null(citizen.IdleSpot);
        Assert.Equal(WorldMap.Places[WorldMap.Camp], citizen.Trip!.To);
    }

    [Fact]
    public void Settle_LeavesAFallenTsarWhereTheyFell()
    {
        var village = NewVillage();
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.March(run, village, [], Now);
        var back = village.TsarTrip!.ArrivesAt + TimeSpan.FromMinutes(1);
        var fallen = village.TsarTrip.PositionAt(back);
        var outcome = new DungeonHeroOutcome(0, 0, [], 0, 0, false, true, EnemyKind.Ghoul);

        DungeonRules.Settle(run, run.Tsar, null, village, outcome, new Random(1), back);

        Assert.Null(village.TsarTrip);
        Assert.Equal(fallen, village.TsarSpot);
        Assert.Equal(back + TimeSpan.FromMinutes(EnemyCatalog.Get(EnemyKind.Ghoul).InjuryMinutes), village.TsarInjuredUntil);
        Assert.Equal("tsar.injured", Assert.Throws<GameRuleException>(() => village.TsarGoTo(WorldMap.Places[WorldMap.Camp], back)).Code);
        Assert.Equal(fallen, village.TsarSpot);
    }

    [Fact]
    public void March_SendsEveryoneToTheEdge_AndTheTsarToo()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);
        var closes = run.GatheringEndsAt;

        DungeonRules.March(run, village, [citizen], closes);

        Assert.Equal(DungeonPhase.Marching, run.Phase);
        Assert.Equal(DungeonRules.MarchSpot, citizen.Trip!.To);
        Assert.Equal(DungeonRules.MarchSpot, village.TsarSpot);
        Assert.True(run.MarchEndsAt <= closes + DungeonRules.LongestMarch);
    }

    [Fact]
    public void March_LeavesTheWoundedWhereTheyFell()
    {
        var village = NewVillage();
        var healthy = NewCitizen(village);
        var wounded = NewCitizen(village);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, healthy, HeroClass.Brawler, Now);
        DungeonRules.Join(run, village, wounded, HeroClass.Brawler, Now);
        wounded.InjuredUntil = Now.AddHours(1);
        wounded.IdleSpot = new TilePoint(12, 8);
        village.TsarInjuredUntil = Now.AddHours(1);
        var tsarSpot = village.TsarSpot;

        DungeonRules.March(run, village, [healthy, wounded], run.GatheringEndsAt);

        Assert.Equal(DungeonPhase.Marching, run.Phase);
        Assert.Equal(DungeonRules.MarchSpot, healthy.Trip!.To);
        Assert.Null(wounded.Trip);
        Assert.Null(wounded.DungeonRunId);
        Assert.Null(run.Hero(wounded.Id));
        Assert.Equal(new TilePoint(12, 8), wounded.IdleSpot);
        Assert.Equal(tsarSpot, village.TsarSpot);
        Assert.True(run.Tsar.Settled);
    }

    [Fact]
    public void ADeadHero_IsNotCarriedOntoTheNextFloor()
    {
        var woods = DungeonCatalog.Parse("woods", "Woods", ["TTTTTTTTTT", "Er.......T", "TTTTTTTTTT"], TimeSpan.FromMinutes(15));
        var barrow = DungeonCatalog.Parse("barrow", "Barrow", ["TTTTTTTTTT", "E.......KT", "TTTTTTTTTT"], TimeSpan.FromMinutes(15));
        var course = DungeonCatalog.Expedition("run", "Run", TimeSpan.FromMinutes(15), woods, barrow);
        var fallen = Fighter(health: 30, damage: 1);
        var survivor = Fighter(health: 200, damage: 1000);
        var simulation = new DungeonSimulation(course, [fallen, survivor]);
        var hero = simulation.Hero(fallen.Id)!;
        hero.State = HeroState.Fighting;
        hero.Health = 0;
        hero.X = 4.5;
        hero.Y = 1.5;
        simulation.Attack(survivor.Id, 0);

        Run(simulation, 2);

        Assert.Equal(1, simulation.Floor);
        Assert.Equal(HeroState.Left, hero.State);
        Assert.Equal(4.5, hero.X);
        Assert.Equal(1.5, hero.Y);
        Assert.Equal(HeroState.Fighting, simulation.Hero(survivor.Id)!.State);
    }
}
