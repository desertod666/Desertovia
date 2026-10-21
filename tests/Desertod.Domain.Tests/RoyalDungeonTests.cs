using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class RoyalDungeonTests
{
    [Fact]
    public void Resident_movement_keeps_its_real_position_and_medical_speed_once_for_both_aliases()
    {
        var v=Village();var owner=NewCitizen(v);owner.AccountId=v.OwnerAccountId;owner.IdleSpot=new(16.5,13.5);owner.Spot=owner.IdleSpot;
        owner.Recovery=new(){Available=true,EffectKind="cast",EffectUntil=Now.AddHours(2)};
        var origin=owner.IdleSpot;var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Ruler",1,Now,[owner]);
        DungeonRules.March(run,v,[owner],run.GatheringEndsAt);
        Assert.Equal(origin,v.TsarTrip!.From);Assert.Equal(.8,v.TsarTrip.SpeedMultiplier);Assert.Equal(.8,owner.Trip!.SpeedMultiplier);
        Assert.Equal(v.TsarTrip.Id,owner.Trip.Id);Assert.Equal(v.TsarTrip.ArrivesAt,owner.Trip.ArrivesAt);Assert.NotSame(v.TsarTrip,owner.Trip);
        DungeonRules.Settle(run,run.Tsar,owner,v,new(0,0,[],0,0,false,false,null),new Random(1),v.TsarTrip.ArrivesAt);
        Assert.Equal(.8,v.TsarTrip!.SpeedMultiplier);Assert.Equal(v.TsarTrip.Id,owner.Trip!.Id);Assert.Equal(WorldMap.Places[WorldMap.Camp],owner.Trip.To);
    }

    [Fact]
    public void Saved_combat_parameters_are_used_after_restore_instead_of_reloading_the_class_catalog()
    {
        var accepted=HeroClassCatalog.Get(HeroClass.Healer) with{Range=2,AttackSeconds=7,Heal=13};
        var fighter=new DungeonFighter(Guid.NewGuid(),Guid.NewGuid(),"Ruler",HeroClass.Voivode,20,1,0,null,true){CombatClass=HeroClass.Healer,CombatDefinition=accepted};
        var sim=new DungeonSimulation(DungeonCatalog.Get(DungeonCatalog.DeadLands),[fighter]);
        var restored=DungeonSimulation.Restore(JsonSerializer.Deserialize<DungeonCheckpoint>(JsonSerializer.Serialize(sim.Capture()))!);
        Assert.Equal(accepted,restored.Heroes[0].Definition);Assert.NotEqual(HeroClassCatalog.Get(HeroClass.Healer),restored.Heroes[0].Definition);
    }

    private static Village Village()
    {
        var v=NewVillage();v.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");return v;
    }

    [Fact]
    public void New_call_borrows_real_gear_and_cancel_returns_each_reserved_piece_once()
    {
        var v=Village();v.Store(ResourceKind.Spear,1);v.Store(ResourceKind.LeatherArmor,1);
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Ruler",1,Now);
        Assert.Equal(1,run.CombatRulesVersion);Assert.Equal(HeroClass.Brawler,run.Tsar.CombatClass);
        Assert.Equal(ResourceKind.LeatherArmor,v.RoyalCombat!.Armor);Assert.Equal(0,v.Stock(ResourceKind.LeatherArmor));
        DungeonRules.PickRoyalClass(run,v,[],HeroClass.Spearman,Now);
        DungeonRules.PickRoyalClass(run,v,[],HeroClass.Spearman,Now);
        Assert.Equal(ResourceKind.Spear,v.RoyalCombat.Weapon);Assert.Equal(0,v.Stock(ResourceKind.Spear));
        DungeonRules.Cancel(run,v,[],Now);
        Assert.Equal(1,v.Stock(ResourceKind.Spear));Assert.Equal(1,v.Stock(ResourceKind.LeatherArmor));
        Assert.Null(v.RoyalCombat.Weapon);Assert.Null(v.RoyalCombat.Armor);Assert.True(run.Tsar.Settled);
        Assert.Throws<GameRuleException>(()=>DungeonRules.Cancel(run,v,[],Now));Assert.Equal(1,v.Stock(ResourceKind.Spear));
    }

    [Fact]
    public void Resident_source_is_reserved_and_a_later_member_never_replaces_an_admitted_standalone_body()
    {
        var v=Village();var owner=NewCitizen(v,(Stat.Endurance,5),(Stat.Strength,7));owner.AccountId=v.OwnerAccountId;
        var bound=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Ruler",1,Now,[owner]);
        Assert.Equal(owner.Id,bound.Tsar.RoyalBodyCitizenId);Assert.Equal(bound.Id,owner.DungeonRunId);Assert.Null(v.RoyalCombat);
        Assert.Same(owner,DungeonRules.RoyalBody(bound,v,[owner],Now)!.Citizen);
        Assert.Null(DungeonRules.RoyalBody(bound,v,[],Now));
        DungeonRules.Cancel(bound,v,[owner],Now);Assert.Null(owner.DungeonRunId);
        v.RoyalCombat=new(){Stats=new(){Strength=3}};
        var standalone=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Ruler",2,Now);
        Assert.Null(standalone.Tsar.RoyalBodyCitizenId);
        Assert.Equal(3,DungeonRules.RoyalBody(standalone,v,[owner],Now)!.Citizen.Stats.Strength);
        v.OwnerAccountId=Guid.NewGuid();Assert.Null(DungeonRules.RoyalBody(standalone,v,[owner],Now));
    }

    [Theory]
    [InlineData(HeroClass.Brawler)]
    [InlineData(HeroClass.Archer)]
    [InlineData(HeroClass.Healer)]
    public void Ordinary_combat_class_has_equal_damage_health_arrow_use_and_cooldown_for_both_roles(HeroClass role)
    {
        var v=Village();var citizen=NewCitizen(v,(Stat.Strength,4),(Stat.Dexterity,3),(Stat.Endurance,5));
        citizen.Weapon=role==HeroClass.Archer?ResourceKind.Bow:null;citizen.Arrows=role==HeroClass.Archer?1:0;citizen.ArrowSharpen=2;
        var definition=HeroClassCatalog.Get(role);var health=DungeonRules.Health(citizen,definition);var damage=DungeonRules.Damage(citizen,definition);
        var map=DungeonCatalog.Parse("equal","Equal",["TTTTTTTTTT","E...r....T","TTTTTTTTTT"],TimeSpan.FromMinutes(10));
        DungeonSimulation Start(bool royal)=>new(map,[new DungeonFighter(Guid.NewGuid(),Guid.NewGuid(),"Same body",royal?HeroClass.Voivode:role,
            health,damage,0,role==HeroClass.Archer?1:null,false){CombatClass=royal?role:null}]);
        var ruler=Start(true);var resident=Start(false);ruler.Attack(ruler.Heroes[0].Id,0);resident.Attack(resident.Heroes[0].Id,0);
        for(var step=0;step<80;step++)
        {
            ruler.Tick(.1,new Random(step));resident.Tick(.1,new Random(step));
            Assert.Equal(resident.Heroes[0].Health,ruler.Heroes[0].Health);Assert.Equal(resident.Heroes[0].Arrows,ruler.Heroes[0].Arrows);
            Assert.Equal(resident.Heroes[0].Definition,ruler.Heroes[0].Definition);Assert.Equal(resident.Foes[0].Health,ruler.Foes[0].Health);
        }
        Assert.Equal(40,health/definition.HealthMultiplier);
        if(role==HeroClass.Archer)Assert.Equal(0,ruler.Heroes[0].Arrows);
        var restored=DungeonSimulation.Restore(JsonSerializer.Deserialize<DungeonCheckpoint>(JsonSerializer.Serialize(ruler.Capture()))!);
        Assert.Equal(role,restored.Heroes[0].Definition.Class);Assert.Equal(ruler.Heroes[0].Health,restored.Heroes[0].Health);
    }

    [Fact]
    public void Early_wound_excludes_the_body_and_returns_equipment_without_experience_or_replacement()
    {
        var v=Village();v.Store(ResourceKind.Spear,1);
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Ruler",1,Now);
        DungeonRules.PickRoyalClass(run,v,[],HeroClass.Spearman,Now);
        v.TsarWoundedAt=Now;v.TsarInjuredUntil=Now.AddHours(1);
        DungeonRules.March(run,v,[],run.GatheringEndsAt);
        Assert.Equal(DungeonOutcome.Abandoned,run.Outcome);Assert.True(run.Tsar.Settled);
        Assert.Equal(1,v.Stock(ResourceKind.Spear));Assert.Null(v.RoyalCombat!.Weapon);Assert.Equal(0,v.RoyalCombat.Stats.StrengthExperience);
    }

    [Fact]
    public void Healer_settlement_consumes_one_remedy_saves_training_and_wound_once_and_preserves_existing_loot_policy()
    {
        var v=Village();v.Store(ResourceKind.Remedy,3);
        v.RoyalCombat=new(){Skills=[new(){Kind=Skill.Scholarship,Level=2}]};
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Ruler",1,Now);
        DungeonRules.PickRoyalClass(run,v,[],HeroClass.Healer,Now);Assert.Equal(1,run.Tsar.Remedies);
        var food=v.Stock(ResourceKind.Food);
        var result=new DungeonHeroOutcome(1,10,[new(ResourceKind.Food,3)],0,0,true,true,EnemyKind.Zombie);
        DungeonRules.Settle(run,run.Tsar,null,v,result,new Random(1),Now);
        var saved=JsonSerializer.Serialize(v.RoyalCombat);var wound=v.TsarInjuredUntil;
        Assert.Equal(2,v.Stock(ResourceKind.Remedy));Assert.True(v.RoyalCombat!.Stats.IntelligenceExperience>0);
        Assert.True(wound>Now);Assert.Null(v.TsarTrip);Assert.Equal(food,v.Stock(ResourceKind.Food));Assert.Empty(run.Tsar.Loot);
        DungeonRules.Settle(run,run.Tsar,null,v,result,new Random(2),Now.AddSeconds(10));
        Assert.Equal(saved,JsonSerializer.Serialize(v.RoyalCombat));Assert.Equal(wound,v.TsarInjuredUntil);Assert.Equal(2,v.Stock(ResourceKind.Remedy));
    }

    [Fact]
    public void An_old_call_keeps_its_class_and_cannot_switch_to_the_new_body_contract()
    {
        var v=Village();var run=new DungeonRun{VillageId=v.Id,DungeonId=DungeonCatalog.DeadLands,Phase=DungeonPhase.Gathering,OpenedAt=Now,GatheringEndsAt=Now.AddMinutes(1),
            Heroes=[new(){AccountId=v.OwnerAccountId,Name="Legacy",Class=HeroClass.Voivode}]};
        Assert.Equal("dungeon.classUnavailable",Assert.Throws<GameRuleException>(()=>DungeonRules.PickRoyalClass(run,v,[],HeroClass.Brawler,Now)).Code);
        Assert.Equal(0,run.CombatRulesVersion);Assert.Null(v.RoyalCombat);Assert.Null(run.Tsar.CombatClass);
    }
}
