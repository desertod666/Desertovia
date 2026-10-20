using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class FarmingTests
{
    private const string FarmPlot = "21,12";

    [Fact]
    public void Farm_GetsItsBeds_AlsoAFarmBuiltBeforeBedsExisted()
    {
        var village = FarmVillage();

        Assert.Equal(FarmCatalog.BedsPerFarm, village.Fields.Count);
        Assert.All(village.Fields, bed => Assert.Null(bed.Crop));
        Assert.False(village.EnsureCamp(Now));
    }

    [Theory]
    [InlineData("sow-turnips", 0)]
    [InlineData("sow-carrots", 2)]
    [InlineData("sow-cabbage", 3)]
    [InlineData("sow-flax", 4)]
    [InlineData("sow-rye", 5)]
    public void EachSeed_NeedsItsFarmingLevel(string taskId, int farming)
    {
        var task = Task(taskId);
        var village = FarmVillage();
        var skilled = NewCitizen(village).WithSkill(Skill.Farming, farming);

        if (farming > 0)
        {
            var novice = NewCitizen(village).WithSkill(Skill.Farming, farming - 1);
            var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(novice, task, WorkContext.For(village), Now));
            Assert.Equal("task.skillRequired", error.Code);
        }

        CycleEngine.Start(skilled, task, WorkContext.For(village), Now);
        var report = Finish(skilled, WorkContext.For(village));

        Assert.False(report.Overflowed);
        Assert.Equal(task.Crop, village.Fields[0].Crop);
    }

    [Fact]
    public void FarmTasks_NeedAFarm()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);

        foreach (var taskId in new[] { "sow-turnips", "water-fields", "harvest" })
        {
            var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task(taskId), WorkContext.For(village), Now));
            Assert.Equal("task.needsBuilding", error.Code);
        }
    }

    [Fact]
    public void DryBed_GrowsSlower_ThanAWateredOne()
    {
        var village = FarmVillage();
        var turnip = FarmCatalog.Get(CropKind.Turnip);
        var dry = Sown(village, 0, CropKind.Turnip);
        var wet = Sown(village, 1, CropKind.Turnip);
        wet.WateredUntil = Now.AddHours(turnip.GrowHours);

        var later = Now.AddHours(turnip.GrowHours / 2);

        Assert.Equal(0.5, FarmRules.GrowthOf(village, wet, later), 6);
        Assert.Equal(0.5 * FarmCatalog.DryGrowthRate, FarmRules.GrowthOf(village, dry, later), 6);
        Assert.True(FarmRules.IsRipe(village, wet, Now.AddHours(turnip.GrowHours)));
        Assert.False(FarmRules.IsRipe(village, dry, Now.AddHours(turnip.GrowHours)));
    }

    [Fact]
    public void Watering_NeedsNoSkill_AndKeepsTheThirstiestBedsWet()
    {
        var village = FarmVillage();
        Sown(village, 0, CropKind.Turnip);
        Sown(village, 1, CropKind.Carrot);
        var citizen = NewCitizen(village);

        CycleEngine.Start(citizen, Task("water-fields"), WorkContext.For(village), Now);
        var end = citizen.CycleEndsAt!.Value;
        var report = Finish(citizen, WorkContext.For(village));

        Assert.False(report.Overflowed);
        Assert.Equal(end + FarmCatalog.WaterDuration, village.Fields[0].WateredUntil);
        Assert.Equal(end + FarmCatalog.WaterDuration, village.Fields[1].WateredUntil);
        Assert.Null(village.Fields[2].WateredUntil);
        Assert.False(FarmRules.IsThirsty(village, village.Fields[0], end));
    }

    [Fact]
    public void Watering_CarriesWaterFromTheRiver()
    {
        var village = FarmVillage();
        Sown(village, 0, CropKind.Turnip);
        var citizen = NewCitizen(village);

        CycleEngine.Start(citizen, Task("water-fields"), WorkContext.For(village), Now);

        Assert.True(citizen.CycleEndsAt!.Value - Now > TimeSpan.FromSeconds(Task("water-fields").WorkSeconds));
    }

    [Fact]
    public void NothingToWater_WhenNoBedIsGrowingOrAllAreWet()
    {
        var village = FarmVillage();
        var citizen = NewCitizen(village);

        Assert.Equal("farm.nothingToWater",
            Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("water-fields"), WorkContext.For(village), Now)).Code);

        Sown(village, 0, CropKind.Turnip).WateredUntil = Now + FarmCatalog.WaterDuration;

        Assert.Equal("farm.nothingToWater",
            Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("water-fields"), WorkContext.For(village), Now)).Code);
    }

    [Fact]
    public void FullFarm_CannotBeSown_UntilABedIsHarvested()
    {
        var village = FarmVillage();
        for (var index = 0; index < FarmCatalog.BedsPerFarm; index++)
        {
            Sown(village, index, CropKind.Turnip);
        }

        var citizen = NewCitizen(village);

        Assert.Equal("farm.noEmptyBed",
            Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("sow-turnips"), WorkContext.For(village), Now)).Code);
    }

    [Fact]
    public void Harvest_EmptiesTheRipeBed_AndBringsItsCrop()
    {
        var village = FarmVillage();
        var rye = FarmCatalog.Get(CropKind.Rye);
        var bed = Sown(village, 0, CropKind.Rye);
        var citizen = NewCitizen(village);

        Assert.Equal("farm.nothingRipe",
            Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("harvest"), WorkContext.For(village), Now)).Code);

        bed.Growth = 1;
        CycleEngine.Start(citizen, Task("harvest"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.False(report.Overflowed);
        Assert.Null(bed.Crop);
        Assert.Equal(rye.Harvest.Select(h => h.Resource), report.Produced.Select(p => p.Resource));
        Assert.True(village.Stock(ResourceKind.Grain) > 0);
    }

    [Fact]
    public void BetterSeeds_GiveABiggerHarvest()
    {
        var turnips = FarmCatalog.Get(CropKind.Turnip).Harvest.Single(h => h.Resource == ResourceKind.Food).Amount;
        var carrots = FarmCatalog.Get(CropKind.Carrot).Harvest.Single(h => h.Resource == ResourceKind.Food).Amount;
        var cabbage = FarmCatalog.Get(CropKind.Cabbage).Harvest.Single(h => h.Resource == ResourceKind.Food).Amount;

        Assert.True(turnips < carrots && carrots < cabbage);
    }

    [Fact]
    public void BedHarvestedByAnother_DoesNotRetargetOrEarnExperience()
    {
        var village = FarmVillage();
        Sown(village, 0, CropKind.Turnip).Growth = 1;
        var citizen = NewCitizen(village);

        CycleEngine.Start(citizen, Task("harvest"), WorkContext.For(village), Now);
        FarmRules.Harvest(village, Now);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.Empty(report.Produced);
        Assert.True(report.Overflowed);
        Assert.Equal(0,report.PrimaryExperience);
    }

    [Fact]
    public void Farming3_IsAFarmer()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village).WithSkill(Skill.Farming, GameCatalog.JourneymanSkill);

        Assert.Equal("farmer", GameCatalog.TitleFor(citizen).Id);
    }

    private static Village FarmVillage()
    {
        var village = NewVillage();
        village.Structures.Add(new Structure { Kind = BuildingKind.Farm, PlotId = FarmPlot, BuiltAt = Now });
        Assert.True(village.EnsureCamp(Now));
        return village;
    }

    private static FieldBed Sown(Village village, int index, CropKind crop)
    {
        var bed = village.Fields.Single(b => b.Index == index);
        bed.Crop = crop;
        bed.Growth = 0;
        bed.UpdatedAt = Now;
        return bed;
    }
}
