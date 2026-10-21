using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class CombatActorTests
{
    [Fact]
    public void The_ruler_cannot_answer_its_own_call_with_a_second_resident_hero()
    {
        var v=NewVillage();var c=NewCitizen(v);c.AccountId=v.OwnerAccountId;
        var run=DungeonRules.Open(v,"dead-lands","Ruler",1,Now);
        Assert.Equal("dungeon.busy",Assert.Throws<GameRuleException>(()=>DungeonRules.Join(run,v,c,HeroClass.Brawler,Now)).Code);
        Assert.Single(run.Heroes);Assert.Null(c.DungeonRunId);
    }

    [Fact]
    public void A_legacy_overlap_keeps_the_raid_bodies_in_place_and_returns_reserved_dungeon_gear()
    {
        var v=NewVillage();var c=NewCitizen(v);
        v.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",Now));v.Store(ResourceKind.Spear,1);
        var run=DungeonRules.Open(v,"dead-lands","Ruler",1,Now);
        DungeonRules.Join(run,v,c,HeroClass.Spearman,Now);
        var before=Trip.Start(WorldMap.Places[WorldMap.Camp],new(20.5,13.5),Now);
        c.Trip=before;v.TsarTrip=before;
        DungeonRules.March(run,v,[c],Now.AddSeconds(61),new HashSet<Guid>{c.AccountId,v.OwnerAccountId});
        Assert.Equal(DungeonPhase.Ended,run.Phase);Assert.Equal(DungeonOutcome.Abandoned,run.Outcome);
        Assert.Same(before,c.Trip);Assert.Same(before,v.TsarTrip);
        Assert.Null(c.DungeonRunId);Assert.Null(c.Weapon);Assert.Equal(1,v.Stock(ResourceKind.Spear));
        Assert.True(run.Tsar.Settled);Assert.Null(run.Hero(c.Id));
    }

    [Fact]
    public void A_dungeon_reservation_stops_automatic_raid_admission_but_does_not_remove_a_legacy_active_fighter()
    {
        var v=NewVillage();var c=NewCitizen(v);var camp=WorldMap.Places[WorldMap.Camp];c.Spot=camp;
        var raid=new Raid{Enemy=EnemyKind.Zombie,Count=1,StrikesAt=Now};
        var battle=new RaidBattle{Id=raid.Id,VillageId=v.Id,Sector="west",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(5),
            Units=[new(){X=camp.X+.5,Y=camp.Y,SpawnX=camp.X+.5,SpawnY=camp.Y,Health=100,MaximumHealth=100,Attack=1,Speed=0,NextAttackAt=Now.AddHours(1)}]};
        var online=new HashSet<Guid>{c.Id};var reserved=new HashSet<Guid>{c.AccountId};
        RaidSimulation.Tick(v,raid,battle,[c],online,Now.AddSeconds(1),reservedAccounts:reserved);
        Assert.Empty(battle.Fighters);
        RaidSimulation.Tick(v,raid,battle,[c],online,Now.AddSeconds(2));
        var f=Assert.Single(battle.Fighters);Assert.True(f.Active);
        var health=f.Health;
        RaidSimulation.Tick(v,raid,battle,[c],online,Now.AddSeconds(3),reservedAccounts:reserved);
        Assert.Same(f,Assert.Single(battle.Fighters));Assert.True(f.Active);Assert.Equal(health,f.Health);
        f.Active=false;f.Mode="withdrawn";
        RaidSimulation.Tick(v,raid,battle,[c],online,Now.AddSeconds(4),reservedAccounts:reserved);
        Assert.False(f.Active);
    }
}
