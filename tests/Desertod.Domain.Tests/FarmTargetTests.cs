using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Rules;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class FarmTargetTests
{
    private static Village Farm()
    {
        var village=NewVillage();village.Structures.Add(new(){Kind=BuildingKind.Farm,PlotId="21,12",BuiltAt=Now});
        FarmRules.EnsureBeds(village);return village;
    }
    private static FarmWorkTarget Target(FieldBed b)=>new(){PlotId=b.PlotId,Index=b.Index,GenerationId=b.GenerationId};
    private static void Ripe(FieldBed bed,CropKind crop){bed.Crop=crop;bed.Growth=1;bed.UpdatedAt=Now;}

    [Fact]
    public void Selected_sow_survives_serialization_and_does_not_pick_the_first_empty_bed()
    {
        var v=Farm();var c=NewCitizen(v);var target=Target(v.Fields[2]);
        CycleEngine.Start(c,Task("sow-turnips"),WorkContext.For(v),Now,farmTarget:target);
        c=JsonSerializer.Deserialize<Citizen>(JsonSerializer.Serialize(c))!;
        Assert.Equal(2,Assert.Single(c.CycleFarmTargets).Index);
        Assert.Equal(target.PlotId,c.CurrentTargetPlotId);
        Finish(c,WorkContext.For(v));Assert.Equal(CropKind.Turnip,v.Fields[2].Crop);
        Assert.NotEqual(target.GenerationId,v.Fields[2].GenerationId);Assert.Null(v.Fields[0].Crop);
        Assert.Null(CycleEngine.Settle(c,WorkContext.For(v),c.CycleEndsAt!.Value.AddHours(1)));
    }

    [Fact]
    public void Harvest_returns_the_selected_crop_instead_of_the_first_ripe_one()
    {
        var v=Farm();Ripe(v.Fields[0],CropKind.Turnip);Ripe(v.Fields[2],CropKind.Rye);var c=NewCitizen(v);
        CycleEngine.Start(c,Task("harvest"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[2]));
        var report=Finish(c,WorkContext.For(v));Assert.Contains(report.Produced,p=>p.Resource==ResourceKind.Grain);
        Assert.Equal(CropKind.Turnip,v.Fields[0].Crop);Assert.Null(v.Fields[2].Crop);
    }

    [Fact]
    public void Old_harvest_cannot_touch_a_new_generation_or_another_ready_bed()
    {
        var v=Farm();Ripe(v.Fields[0],CropKind.Turnip);Ripe(v.Fields[2],CropKind.Turnip);var c=NewCitizen(v);var old=Target(v.Fields[2]);
        CycleEngine.Start(c,Task("harvest"),WorkContext.For(v),Now,farmTarget:old);
        FarmRules.Harvest(v,Now,target:old);Assert.True(FarmRules.Sow(v,CropKind.Carrot,Now,target:Target(v.Fields[2])));
        v.Fields[2].Growth=1;
        var report=Finish(c,WorkContext.For(v));Assert.Empty(report.Produced);Assert.Equal(0,report.PrimaryExperience);
        Assert.Equal(CropKind.Carrot,v.Fields[2].Crop);Assert.Equal(CropKind.Turnip,v.Fields[0].Crop);
        var other=NewCitizen(v);
        Assert.Equal("farm.stale",Assert.Throws<GameRuleException>(()=>CycleEngine.Start(other,Task("harvest"),WorkContext.For(v),Now,farmTarget:old)).Code);
    }

    [Fact]
    public void Reservation_covers_overdue_work_and_allows_another_bed()
    {
        var v=Farm();var a=NewCitizen(v);var b=NewCitizen(v);
        CycleEngine.Start(a,Task("sow-turnips"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[0]));
        var context=new WorkContext{Village=v,FarmWork=[new(a.Id,null,a.CycleFarmTargets)]};
        Assert.Equal("farm.busy",Assert.Throws<GameRuleException>(()=>CycleEngine.Start(b,Task("sow-turnips"),context,Now.AddHours(2),farmTarget:Target(v.Fields[0]))).Code);
        CycleEngine.Start(b,Task("sow-turnips"),context,Now.AddHours(2),farmTarget:Target(v.Fields[2]));
        Finish(b,context);Assert.Equal(CropKind.Turnip,v.Fields[2].Crop);Assert.Null(v.Fields[0].Crop);
    }

    [Fact]
    public void Explicit_watering_only_reaches_the_selected_bed()
    {
        var v=Farm();foreach(var bed in v.Fields){bed.Crop=CropKind.Turnip;bed.UpdatedAt=Now;}
        var c=NewCitizen(v);CycleEngine.Start(c,Task("water-fields"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[2]));
        Finish(c,WorkContext.For(v));Assert.NotNull(v.Fields[2].WateredUntil);Assert.Null(v.Fields[0].WateredUntil);Assert.Null(v.Fields[1].WateredUntil);
    }

    [Fact]
    public void Rebuilt_farm_on_the_same_plot_does_not_inherit_a_pending_sow_target()
    {
        var v=Farm();var c=NewCitizen(v);CycleEngine.Start(c,Task("sow-turnips"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[0]));
        v.Fields.Clear();FarmRules.EnsureBeds(v);
        Assert.True(Finish(c,WorkContext.For(v)).Overflowed);Assert.All(v.Fields,b=>Assert.Null(b.Crop));
    }

    [Fact]
    public void Legacy_unaddressed_cycle_keeps_its_previous_settlement_rules()
    {
        var v=Farm();Ripe(v.Fields[0],CropKind.Turnip);var c=NewCitizen(v);
        CycleEngine.Start(c,Task("harvest"),WorkContext.For(v),Now);
        c.CycleFarmTargets=[]; // Existing database cycles do not have the new target column.
        FarmRules.Harvest(v,Now);var report=Finish(c,WorkContext.For(v));
        Assert.Equal(GameRules.BaseExperiencePerCycle*GameRules.OverflowExperienceMultiplier,report.PrimaryExperience,6);
    }
}
