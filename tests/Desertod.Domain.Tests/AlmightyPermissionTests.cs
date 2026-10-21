using Desertod.Domain.Combat;
using Desertod.Domain.Director;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;

namespace Desertod.Domain.Tests;

public sealed class AlmightyPermissionTests
{
    [Fact]
    public void Category_filter_keeps_observation_and_cannot_admit_unknown_or_malformed_actions()
    {
        AlmightyOption[] options=[new("none","Watch","Observe"),Advice,new("dungeon","Call","Gather"),
            new("vigor_stronger","Tonic","Tune"),new("skeletons_small","Wave","Raid",EnemyKind.Skeleton)];
        Assert.Equal(["none"],AlmightyPermissions.Filter(options,AlmightyPermissions.ControllableKinds.ToArray()).Select(o=>o.Id));
        Assert.Equal(["none","economy_report","dungeon","vigor_stronger"],AlmightyPermissions.Filter(options,["raid"]).Select(o=>o.Id));
        Assert.False(AlmightyPermissions.IsEnabled("wallet_credit",[]));
        Assert.Throws<ArgumentException>(()=>AlmightyPermissions.Filter([new("skeletons_small","Wave","Wrong enemy",EnemyKind.Zombie)],["raid"]));
    }
    private static readonly AlmightyFacts Facts=new(5,3,3,0,0,1,0,0,false,false);
    private static AlmightyObservedWorld World=>new(Facts,new(3,0,12,16),new Dictionary<string,int>{{"Food",7},{"Wood",4}},"Propose",8,720);
    private static AlmightyOption Advice=>new("economy_report","Advice","No currency changes");

    [Fact]
    public void Readiness_budget_uses_the_same_rest_admission_as_the_actual_fight()
    {
        var village=TestWorld.NewVillage();var citizen=TestWorld.NewCitizen(village,(Desertod.Domain.Stats.Stat.Strength,10));
        IReadOnlySet<Guid> online=new HashSet<Guid>{citizen.Id};
        var empty=RaidBalance.Assess(village,[],online,TestWorld.Now);
        var active=RaidBalance.Assess(village,[citizen],online,TestWorld.Now);Assert.Equal(1,active.Guards);Assert.True(active.Budget>empty.Budget);
        citizen.RestState=RestState.Resting;Assert.False(RaidSimulation.CanFight(citizen,TestWorld.Now));
        Assert.Equal(empty,RaidBalance.Assess(village,[citizen],online,TestWorld.Now));
        citizen.RestState=RestState.RestRequested;Assert.True(RaidSimulation.CanFight(citizen,TestWorld.Now));
        Assert.Equal(active,RaidBalance.Assess(village,[citizen],online,TestWorld.Now));
        SafeRest.Wake(citizen);Assert.Equal(active,RaidBalance.Assess(village,[citizen],online,TestWorld.Now));
    }

    [Fact]
    public void Current_catalog_is_closed_and_does_not_grant_autonomous_execution()
    {
        var options=AlmightyPolicy.Options(Facts,[new("north",[])],1200).Concat(VigorTuning.Options(new Village{Slug="permissions",Name="Permissions"})).ToArray();
        var permissions=AlmightyPermissions.Describe(options);
        Assert.Equal(options.Length,permissions.Count);
        Assert.All(permissions,p=>Assert.False(p.Autonomous));
        Assert.All(permissions.Where(p=>p.OptionId!="none"),p=>Assert.True(p.RequiresOwnerApproval));
        Assert.Equal("advice",Assert.Single(permissions,p=>p.OptionId=="economy_report").Kind);
        Assert.DoesNotContain(permissions,p=>p.Kind is "tax" or "wallet" or "subscription");
    }

    [Fact]
    public void Unknown_kinds_mismatched_raid_parameters_and_duplicate_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(()=>AlmightyPermissions.Describe([new("wallet_credit","Money","Mint")]));
        Assert.Throws<ArgumentException>(()=>AlmightyPermissions.Describe([Advice with{Sector="north"}]));
        Assert.Throws<ArgumentException>(()=>AlmightyPermissions.Describe([Advice,Advice]));
        Assert.Throws<ArgumentException>(()=>AlmightyPermissions.Describe([new("skeletons_small","Wave","Wrong enemy",EnemyKind.Zombie)]));
        Assert.Throws<ArgumentException>(()=>AlmightyPermissions.Describe([new("skeletons_north","Wave","Wrong sector",EnemyKind.Skeleton,"south","mixed")]));
        Assert.Throws<ArgumentException>(()=>AlmightyPermissions.Describe([null!]));
    }

    [Fact]
    public void Observed_revision_is_stable_under_order_changes_and_changes_with_its_named_evidence()
    {
        AlmightyOption[] options=[Advice,new("none","Watch","Observe")];
        var revision=AlmightyPermissions.Revision(World,options);
        Assert.Equal(64,revision.Length);
        Assert.Equal(revision,AlmightyPermissions.Revision(World with{Stock=new Dictionary<string,int>{{"Wood",4},{"Food",7}}},options.Reverse().ToArray()));
        Assert.Equal(revision,AlmightyPermissions.Revision(World,[Advice with{Title="Translation"},options[1]]));
        Assert.NotEqual(revision,AlmightyPermissions.Revision(World with{Stock=new Dictionary<string,int>{{"Food",6},{"Wood",4}}},options));
        Assert.NotEqual(revision,AlmightyPermissions.Revision(World with{Readiness=new(3,0,12,17)},options));
        Assert.NotEqual(revision,AlmightyPermissions.Revision(World with{Mode="Observe"},options));
        Assert.NotEqual(revision,AlmightyPermissions.Revision(World,[Advice]));
    }
}
