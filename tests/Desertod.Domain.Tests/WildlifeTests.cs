using Desertod.Domain.Catalog;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class WildlifeTests
{
    private static Desertod.Domain.Entities.Village HuntingVillage()
    {
        // Hunting rules use a fixed, reachable terrain fixture; navigation is verified separately.
        var village=NewVillage();village.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");
        village.Wilds=Wilderness.Generate(village.Id);return village;
    }

    [Fact]
    public void NewVillage_HasFullHerdsOfEveryAnimal()
    {
        var village = NewVillage();

        foreach (var animal in WildlifeCatalog.Animals)
        {
            Assert.Equal(animal.Capacity, village.HerdOf(animal.Kind).Current(Now));
        }
    }

    [Fact]
    public void OldVillage_GetsItsHerdsOnce()
    {
        var village = NewVillage();
        village.Wildlife.Clear();

        Assert.True(village.EnsureCamp(Now));
        Assert.Equal(WildlifeCatalog.Animals.Count, village.Wildlife.Count);
        Assert.False(village.EnsureCamp(Now));
    }

    [Fact]
    public void HuntingRabbits_TakesThemFromTheHerd_AndBringsMeatAndHides()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var herd = village.HerdOf(AnimalKind.Rabbit);

        CycleEngine.Start(citizen, Task("hunt"), WorkContext.For(village), Now);
        var end = citizen.CycleEndsAt!.Value;
        var report = Finish(citizen, WorkContext.For(village));

        var caught = herd.Definition.Capacity - herd.Current(end);
        Assert.True(caught > 0);
        var share = (double)caught / Task("hunt").Hunt!.Animals;
        Assert.Equal(YieldMath.Whole(6 * share), report.Produced.Single(p => p.Resource == ResourceKind.Food).Amount);
        Assert.Equal(YieldMath.Whole(1 * share), village.Stock(ResourceKind.Leather));
        Assert.False(report.Overflowed);
    }

    [Fact]
    public void HuntingBirds_BringsFeathers()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village, (Stat.Dexterity, 3)).WithSkill(Skill.Hunting, 3);

        CycleEngine.Start(citizen, Task("hunt-birds"), WorkContext.For(village), Now);
        Finish(citizen, WorkContext.For(village));

        Assert.True(village.Stock(ResourceKind.Feathers) > 0);
    }

    [Theory]
    [InlineData("hunt", 0)]
    [InlineData("track-game", 2)]
    [InlineData("hunt-birds", 3)]
    public void EachPrey_NeedsItsHuntingLevel(string taskId, int hunting)
    {
        var task = Task(taskId);
        var village = HuntingVillage();
        var skilled = NewCitizen(village).WithSkill(Skill.Hunting, hunting);

        if (hunting > 0)
        {
            var novice = NewCitizen(village).WithSkill(Skill.Hunting, hunting - 1);
            var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(novice, task, WorkContext.For(village), Now));
            Assert.Equal("task.skillRequired", error.Code);
        }

        CycleEngine.Start(skilled, task, WorkContext.For(village), Now);
        Finish(skilled, WorkContext.For(village));
        var prey = WildlifeCatalog.Get(task.Hunt!.Prey);
        Assert.True(village.HerdOf(prey.Kind).Current(skilled.CycleEndsAt!.Value) < prey.Capacity);
    }

    [Fact]
    public void HuntingExperience_FollowsTheHuntingTime()
    {
        var experience = new Dictionary<string, int>();
        foreach (var taskId in new[] { "hunt", "track-game", "hunt-birds" })
        {
            var village = HuntingVillage();
            var citizen = NewCitizen(village, (Stat.Dexterity, 3)).WithSkill(Skill.Hunting, 3);
            CycleEngine.Start(citizen, Task(taskId), WorkContext.For(village), Now);
            experience[taskId] = Finish(citizen, WorkContext.For(village)).PrimaryExperience;
        }

        Assert.Equal(GameRules.BaseExperiencePerCycle, experience["hunt"]);
        Assert.Equal(GameRules.BaseExperiencePerCycle * 2, experience["track-game"]);
        Assert.Equal(GameRules.BaseExperiencePerCycle / 2, experience["hunt-birds"]);
    }

    [Fact]
    public void ScarceHerd_GivesASmallerCatch()
    {
        var abundant = NewVillage();
        var scarce = NewVillage();
        var rabbits = scarce.HerdOf(AnimalKind.Rabbit);
        rabbits.Count = (int)(rabbits.Definition.Capacity * WildlifeCatalog.AbundantShare / 2);

        var full = Food(Hunt(abundant));
        var citizen = NewCitizen(scarce, (Stat.Dexterity, GameRules.StatMaximum));
        CycleEngine.Start(citizen, Task("hunt"), WorkContext.For(scarce), Now);
        var abundance = rabbits.Abundance(citizen.CycleEndsAt!.Value);
        var thin = Food(Finish(citizen, WorkContext.For(scarce)));

        Assert.InRange(abundance, 0.5, 0.6);
        Assert.InRange(thin, full * abundance - 2, full * abundance + 2);
        Assert.True(thin < full);
    }

    [Fact]
    public void HuntedOutLand_CannotBeHunted_UntilAnimalsComeBack()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var rabbits = village.HerdOf(AnimalKind.Rabbit);
        rabbits.Count = 0;
        rabbits.UpdatedAt = Now;

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("hunt"), WorkContext.For(village), Now));
        Assert.Equal("hunt.noAnimals", error.Code);

        var later = Now.AddHours(rabbits.Definition.RecoveryHalfLifeHours);
        Assert.Equal(rabbits.Definition.Capacity / 2, rabbits.Current(later));
        CycleEngine.Start(citizen, Task("hunt"), WorkContext.For(village), later);
    }

    [Fact]
    public void LastAnimalsCaughtByAnother_LeaveAnEmptyHunt_WithHalfExperience()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var rabbits = village.HerdOf(AnimalKind.Rabbit);
        rabbits.Count = WildlifeCatalog.MinimumToHunt;
        rabbits.UpdatedAt = Now;

        CycleEngine.Start(citizen, Task("hunt"), WorkContext.For(village), Now);
        rabbits.Take(rabbits.Current(citizen.CycleEndsAt!.Value), citizen.CycleEndsAt!.Value);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.Empty(report.Produced);
        Assert.True(report.Overflowed);
        Assert.Equal(YieldMath.Whole(GameRules.BaseExperiencePerCycle * GameRules.OverflowExperienceMultiplier), report.PrimaryExperience);
    }

    private static CycleReport Hunt(Entities.Village village)
    {
        var citizen = NewCitizen(village, (Stat.Dexterity, GameRules.StatMaximum));
        CycleEngine.Start(citizen, Task("hunt"), WorkContext.For(village), Now);
        return Finish(citizen, WorkContext.For(village));
    }

    private static int Food(CycleReport report) => report.Produced.Single(p => p.Resource == ResourceKind.Food).Amount;
}
