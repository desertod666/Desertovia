using System.Text.Json;
using System.Text.Json.Nodes;
using Desertod.Domain.Catalog;
using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class MortalRoyalCombatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remedy_ends_a_rescue_at_the_actual_pickup_stage_without_teleport_or_delivery_reward(bool pickedUp)
    {
        var v=NewVillage();v.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");
        v.Structures.Add(new(){Kind=BuildingKind.Hospital,PlotId="20,15",Level=1,BuiltAt=Now});
        var helper=NewCitizen(v);v.TsarSpot=new(16.5,16.5);v.TsarWoundedAt=Now;v.TsarInjuredUntil=Now.AddMinutes(40);
        var route=RescueRules.PrepareRoute(v,new(15.5,13.5),v.TsarSpot.Value,v.OwnerAccountId,true,Now,Now);
        helper.RescueJourney=route;helper.RescuePatientId=v.OwnerAccountId;v.TsarRescueByCitizenId=helper.Id;
        var at=pickedUp?route.PickupAt.AddTicks((route.ArrivesAt-route.PickupAt).Ticks/2):Now;
        var expected=pickedUp?route.PositionAt(at):v.TsarSpot.Value;
        Assert.Equal("heal.none",Assert.Throws<GameRuleException>(()=>RoyalInjury.Heal(v,[helper],at)).Code);
        Assert.Null(route.CompletedAt);Assert.Equal(helper.Id,v.TsarRescueByCitizenId);Assert.NotNull(helper.RescuePatientId);
        v.Store(ResourceKind.Remedy,1);RoyalInjury.Heal(v,[helper],at);
        Assert.Null(v.TsarInjuredUntil);Assert.Null(v.TsarRescueByCitizenId);Assert.Null(helper.RescuePatientId);
        Assert.Equal(expected,v.TsarSpot);Assert.Equal(route.PositionAt(at),helper.Spot);Assert.Equal(at,route.CompletedAt);
        Assert.False(route.Delivered);Assert.Equal(0,helper.Renown);Assert.Equal(0,v.Stock(ResourceKind.Remedy));
        Assert.False(RescueRules.AdmitTsar(helper,v,route.ArrivesAt));Assert.Equal(expected,v.TsarSpot);
    }

    [Fact]
    public void Recovered_native_hospital_patient_can_march_without_a_preceding_state_read()
    {
        var v=NewVillage();v.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");
        var run=DungeonRules.Open(v,DungeonCatalog.DeadLands,"Ruler",123,Now);
        v.TsarWoundedAt=Now;v.TsarInjuredUntil=Now.AddSeconds(10);v.TsarHospitalizedAt=Now;
        DungeonRules.March(run,v,[],run.GatheringEndsAt);
        Assert.Equal(DungeonPhase.Marching,run.Phase);Assert.False(run.Tsar.Settled);Assert.NotNull(v.TsarTrip);
        Assert.Null(v.TsarInjuredUntil);Assert.Null(v.TsarHospitalizedAt);
    }

    private static (Village V,Citizen C,Raid Raid,RaidBattle Battle,RaidFighter Fighter) Fight(bool royal,ResourceKind? weapon=null,int arrows=0,bool legacy=false)
    {
        var v=NewVillage();v.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");
        var c=NewCitizen(v,(Stat.Strength,4),(Stat.Dexterity,3),(Stat.Endurance,5)).WithSkill(Skill.Guarding,1);
        c.Weapon=weapon;c.Armor=ResourceKind.LeatherArmor;c.Arrows=arrows;c.ArrowSharpen=arrows>0?2:0;c.WeaponSharpen=weapon is null?0:1;
        c.Spot=new(15.5,13.5);c.CycleSettled=true;
        if(royal)v.RoyalCombat=new(){Stats=c.Stats,Skills=c.Skills,Weapon=weapon,Armor=c.Armor,Arrows=arrows,ArrowSharpen=c.ArrowSharpen,WeaponSharpen=c.WeaponSharpen};
        var raid=new Raid{Enemy=EnemyKind.Zombie,Count=1,StrikesAt=Now};
        var f=new RaidFighter{IsTsar=royal,AccountId=royal?v.OwnerAccountId:null,CitizenId=royal?Guid.Empty:c.Id,
            CombatRulesVersion=legacy?null:1,X=15.5,Y=13.5,Health=legacy?80:CombatantRules.RaidHealth(c),
            MaximumHealth=legacy?80:CombatantRules.RaidHealth(c),Active=true,Mode="hold",NextAttackAt=Now};
        var b=new RaidBattle{Id=raid.Id,VillageId=v.Id,Sector="west",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(5),
            CombatRulesVersion=legacy?0:1,Seed=123,Fighters=[f],Units=[new(){X=16,Y=13.5,SpawnX=16,SpawnY=13.5,Health=100,MaximumHealth=100,Attack=7,Speed=0,NextAttackAt=Now.AddHours(1)}]};
        return(v,c,raid,b,f);
    }
    private static void Tick((Village V,Citizen C,Raid Raid,RaidBattle Battle,RaidFighter Fighter) s,bool royal,int seconds)=>
        RaidSimulation.Tick(s.V,s.Raid,s.Battle,royal?[]:[s.C],new HashSet<Guid>(),Now.AddSeconds(seconds));

    [Theory]
    [InlineData(null,0)]
    [InlineData(ResourceKind.Spear,0)]
    [InlineData(ResourceKind.Bow,1)]
    public void Equal_bodies_have_equal_health_damage_cooldown_protection_and_actual_arrow_spending(ResourceKind? weapon,int arrows)
    {
        var ruler=Fight(true,weapon,arrows);var resident=Fight(false,weapon,arrows);
        ruler.Battle.Units[0].NextAttackAt=Now;resident.Battle.Units[0].NextAttackAt=Now;
        for(var second=1;second<=4;second++)
        {
            Tick(ruler,true,second);Tick(resident,false,second);
            Assert.Equal(resident.Fighter.Health,ruler.Fighter.Health);
            Assert.Equal(resident.Fighter.DamageDealt,ruler.Fighter.DamageDealt);
            Assert.Equal(resident.Fighter.DamageTaken,ruler.Fighter.DamageTaken);
            Assert.Equal(resident.Fighter.NextAttackAt,ruler.Fighter.NextAttackAt);
            Assert.Equal(resident.Battle.Units[0].Health,ruler.Battle.Units[0].Health);
            Assert.Equal(resident.C.Arrows,ruler.V.RoyalCombat!.Arrows);
        }
        Assert.Equal(52,ruler.Fighter.MaximumHealth);Assert.True(ruler.Fighter.DamageDealt>0);
        if(arrows>0){Assert.Equal(1,ruler.Fighter.ArrowsSpent);Assert.Equal(0,ruler.V.RoyalCombat!.ArrowSharpen);}
    }

    [Fact]
    public void A_missing_rules_version_keeps_an_announced_legacy_royal_body_and_its_original_attack()
    {
        var s=Fight(true,legacy:true);
        var json=JsonNode.Parse(JsonSerializer.Serialize(s.Battle))!.AsObject();json.Remove("CombatRulesVersion");
        json["Fighters"]![0]!.AsObject().Remove("CombatRulesVersion");
        var loaded=JsonSerializer.Deserialize<RaidBattle>(json.ToJsonString())!;
        Assert.Equal(0,loaded.CombatRulesVersion);Assert.Null(loaded.Fighters[0].CombatRulesVersion);
        s=(s.V,s.C,s.Raid,loaded,loaded.Fighters[0]);
        Tick(s,true,1);Tick(s,true,2);
        Assert.Equal(80,s.Fighter.MaximumHealth);Assert.Equal(16,s.Fighter.DamageDealt);Assert.Equal(84,s.Battle.Units[0].Health);
        Assert.Equal(0,s.V.RoyalCombat!.Stats.StrengthExperience);
    }

    [Fact]
    public void Both_roles_fall_at_zero_health_and_receive_the_same_finite_wound_without_resurrection()
    {
        var ruler=Fight(true);var resident=Fight(false);
        foreach(var s in new[]{ruler,resident}){s.Fighter.Health=1;s.Battle.Units[0].Attack=1000;s.Battle.Units[0].NextAttackAt=Now;}
        Tick(ruler,true,1);Tick(resident,false,1);
        Assert.Equal(0,ruler.Fighter.Health);Assert.True(ruler.Fighter.Wounded);Assert.False(ruler.Fighter.Active);
        Assert.Equal(resident.C.InjuredUntil,ruler.V.TsarInjuredUntil);Assert.True(ruler.V.TsarInjuredUntil>Now);
        var damage=ruler.Fighter.DamageDealt;Tick(ruler,true,2);Assert.Equal(damage,ruler.Fighter.DamageDealt);Assert.Equal(0,ruler.Fighter.Health);
    }

    [Fact]
    public void Training_and_equipment_wear_settle_once_for_a_new_royal_body()
    {
        var s=Fight(true,ResourceKind.Spear);s.Battle.Units[0].Health=1;
        Tick(s,true,1);Assert.NotNull(s.Battle.FinishedAt);Assert.True(s.Fighter.RewardsGranted);
        var before=JsonSerializer.Serialize(s.V.RoyalCombat);
        Assert.True(s.V.RoyalCombat!.Skills.Single(p=>p.Kind==Skill.Guarding).Experience>0);
        Assert.True(s.V.RoyalCombat.Stats.StrengthExperience+s.V.RoyalCombat.Stats.DexterityExperience>0);
        Tick(s,true,2);Assert.Equal(before,JsonSerializer.Serialize(s.V.RoyalCombat));
    }

    [Fact]
    public void Native_hospital_care_ends_with_the_finite_wound_and_aliases_do_not_create_a_second_rescue_reward()
    {
        var v=NewVillage();var c=NewCitizen(v);c.AccountId=v.OwnerAccountId;
        CombatRules.Injure(c,TimeSpan.FromMinutes(10),Now,true);
        v.TsarWoundedAt=c.WoundedAt;v.TsarInjuredUntil=Now.AddMinutes(7);v.TsarHospitalizedAt=Now;
        RoyalInjury.Refresh(v,Now.AddMinutes(1),[c]);Assert.Equal(v.TsarInjuredUntil,c.InjuredUntil);Assert.False(c.RescueRewardEligible);
        v.TsarGoTo(new(20.5,13.5),Now.AddMinutes(8));Assert.Null(v.TsarHospitalizedAt);Assert.Null(v.TsarInjuredUntil);Assert.NotNull(v.TsarTrip);
    }

    [Fact]
    public void An_admitted_source_cannot_change_to_a_new_resident_or_fall_back_to_a_new_body()
    {
        var v=NewVillage();var resident=NewCitizen(v,(Stat.Strength,99));resident.AccountId=v.OwnerAccountId;
        v.RoyalCombat=new(){Stats=new(){Strength=3}};
        Assert.Same(resident,RoyalCombatBody.Resolve(v,[resident],Now).Citizen);
        var standalone=new RaidFighter{IsTsar=true,CitizenId=Guid.Empty,CombatRulesVersion=1};
        Assert.Equal(3,RoyalCombatBody.ForFighter(v,standalone,[resident],Now)!.Citizen.Stats.Strength);
        var bound=new RaidFighter{IsTsar=true,CitizenId=resident.Id,CombatRulesVersion=1};
        Assert.Same(resident,RoyalCombatBody.ForFighter(v,bound,[resident],Now)!.Citizen);
        Assert.Null(RoyalCombatBody.ForFighter(v,bound,[],Now));
    }

    [Fact]
    public void A_new_live_body_can_borrow_a_bow_with_one_real_arrow_without_changing_aggregate_legacy_rules()
    {
        var v=NewVillage();v.Store(ResourceKind.Bow,1);v.Store(ResourceKind.Arrows,1,sharpen:2);
        var c=NewCitizen(v);var bow=v.Stock(ResourceKind.Bow);
        CombatRules.Arm(c,v);Assert.Null(c.Weapon);Assert.Equal(bow,v.Stock(ResourceKind.Bow));
        CombatRules.Arm(c,v,liveCombat:true);
        Assert.Equal(ResourceKind.Bow,c.Weapon);Assert.Equal(1,c.Arrows);Assert.Equal(2,c.ArrowSharpen);
        Assert.Equal(0,v.Stock(ResourceKind.Bow));Assert.Equal(0,v.Stock(ResourceKind.Arrows));
        Assert.Null(CombatRules.UsableWeapon(c));Assert.NotNull(CombatantRules.LiveWeapon(c));
    }

    [Fact]
    public void Battle_healing_uses_the_same_fraction_cooldown_and_one_real_remedy_per_action()
    {
        var s=Fight(true);s.Fighter.Health=10;
        s.V.Store(ResourceKind.Remedy,3);var stock=s.V.Stock(ResourceKind.Remedy);
        var body=RoyalCombatBody.ForFighter(s.V,s.Fighter,[],Now)!;var id=Guid.NewGuid();
        Assert.True(BattleActions.Heal(s.V,body.Citizen,s.Fighter,id,Now));body.Commit();
        Assert.Equal(10+52*.2,s.Fighter.Health);Assert.Equal(stock-1,s.V.Stock(ResourceKind.Remedy));
        Assert.False(BattleActions.Heal(s.V,body.Citizen,s.Fighter,id,Now));
        Assert.Equal("battle.healCooldown",Assert.Throws<GameRuleException>(()=>BattleActions.Heal(s.V,body.Citizen,s.Fighter,Guid.NewGuid(),Now)).Code);
        Assert.Equal(stock-1,s.V.Stock(ResourceKind.Remedy));
    }
}
