using System.Text.Json;
using System.Text.Json.Nodes;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Stats;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class AmbushSceneTests
{
    private static string State(DungeonSimulation fight)=>JsonSerializer.Serialize(fight.Capture());
    private static DungeonSimulation Reload(DungeonSimulation fight)=>DungeonSimulation.Restore(JsonSerializer.Deserialize<DungeonCheckpoint>(State(fight))!);

    [Theory]
    [InlineData(EnemyKind.Wolf)]
    [InlineData(EnemyKind.Boar)]
    [InlineData(EnemyKind.Bear)]
    [InlineData(EnemyKind.Zombie)]
    [InlineData(EnemyKind.Skeleton)]
    public void World_enemies_have_one_reachable_manual_scene_without_granting_any_reward(EnemyKind kind)
    {
        var person=NewCitizen(NewVillage());var arrows=person.Arrows;var weapon=person.Weapon;
        var fight=AmbushScene.Create(person,kind);var hero=Assert.Single(fight.Heroes);
        Assert.Equal(kind,Assert.Single(fight.Foes).Kind);Assert.Null(hero.TargetFoe);Assert.False(fight.IsOver);
        fight.Tick(.1,DungeonRandom.FromSeed(7));Assert.False(fight.IsOver);Assert.Null(hero.TargetFoe);
        Assert.Empty(hero.Loot);Assert.Equal(0,hero.Experience);Assert.Equal(arrows,person.Arrows);Assert.Equal(weapon,person.Weapon);
        Assert.False(fight.MoveTo(person.Id,5.5,2.5));Assert.False(fight.MoveTo(person.Id,double.NaN,4));
        Assert.True(fight.Attack(person.Id,0));Assert.Equal(0,hero.TargetFoe);
        Assert.Equal(State(fight),State(Reload(fight)));
    }

    [Fact]
    public void Manually_won_scene_has_an_outcome_but_no_parallel_loot_or_experience_payment()
    {
        var person=NewCitizen(NewVillage(),(Stat.Strength,50));var fight=AmbushScene.Create(person,EnemyKind.Wolf);
        Assert.True(fight.Attack(person.Id,0));var random=DungeonRandom.FromSeed(4);
        for(var i=0;i<600&&!fight.IsOver;i++)fight.Tick(.1,random);
        Assert.Equal(DungeonOutcome.Victory,fight.Outcome);var outcome=fight.OutcomeOf(fight.Hero(person.Id)!);
        Assert.Equal(1,outcome.Kills);Assert.Empty(outcome.Loot);Assert.Equal(0,outcome.Experience);Assert.False(outcome.KnockedOut);
    }

    [Fact]
    public void Retreat_remains_vulnerable_walks_to_the_exit_and_survives_checkpoint_reload()
    {
        var person=NewCitizen(NewVillage());var definition=AmbushScene.Map(EnemyKind.Wolf);
        var fighter=AmbushScene.Fighter(person) with{Health=1000};var fight=new DungeonSimulation(definition,[fighter]);
        var hero=fight.Hero(person.Id)!;hero.X=6.5;hero.Y=4.5;fight.Foes[0].X=6.7;
        Assert.True(fight.Retreat(person.Id));var before=State(fight);Assert.True(fight.Retreat(person.Id));Assert.Equal(before,State(fight));
        Assert.False(fight.Attack(person.Id,0));Assert.False(fight.MoveTo(person.Id,9,4));
        var random=DungeonRandom.FromSeed(2);fight.Tick(.1,random);
        Assert.Equal(HeroState.Fighting,hero.State);Assert.True(hero.Health<1000);Assert.False(fight.IsOver);
        var restored=Reload(fight);var next=new DungeonRandom(random.State);Assert.True(restored.Hero(person.Id)!.RetreatRequested);
        for(var i=0;i<100&&!fight.IsOver;i++)
        {
            fight.Tick(.1,random);restored.Tick(.1,next);Assert.Equal(State(fight),State(restored));
        }
        Assert.True(fight.IsOver);Assert.Equal(HeroState.Left,hero.State);Assert.True(hero.X<1.1);
        Assert.False(fight.OutcomeOf(hero).KnockedOut);Assert.Empty(fight.OutcomeOf(hero).Loot);
    }

    [Fact]
    public void Falling_during_retreat_remains_a_real_combat_defeat()
    {
        var person=NewCitizen(NewVillage());var fight=new DungeonSimulation(AmbushScene.Map(EnemyKind.Bear),[AmbushScene.Fighter(person) with{Health=1}]);
        var hero=fight.Hero(person.Id)!;hero.X=6.5;hero.Y=4.5;fight.Foes[0].X=6.7;
        Assert.True(fight.Retreat(person.Id));fight.Tick(.1,DungeonRandom.FromSeed(1));
        Assert.True(fight.OutcomeOf(hero).KnockedOut);Assert.False(hero.ExternallyWithdrawn);Assert.Equal(EnemyKind.Bear,hero.Killer);
    }

    [Fact]
    public void Old_dungeon_checkpoint_without_retreat_or_scene_catalog_keeps_loading()
    {
        var person=NewCitizen(NewVillage());var fight=new DungeonSimulation(DungeonCatalog.Get(DungeonCatalog.DeadLands),[AmbushScene.Fighter(person)]);
        var old=JsonNode.Parse(State(fight))!;old["Definition"]!.AsObject().Remove("FoeDefinitions");
        foreach(var h in old["Heroes"]!.AsArray())h!.AsObject().Remove("RetreatRequested");
        var resumed=DungeonSimulation.Restore(JsonSerializer.Deserialize<DungeonCheckpoint>(old.ToJsonString())!);
        Assert.False(resumed.Hero(person.Id)!.RetreatRequested);Assert.Equal(fight.Foes.Count,resumed.Foes.Count);
    }
}
