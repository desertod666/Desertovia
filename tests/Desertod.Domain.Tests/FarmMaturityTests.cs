using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class FarmMaturityTests
{
    [Theory]
    [InlineData(CropKind.Turnip)]
    [InlineData(CropKind.Carrot)]
    [InlineData(CropKind.Cabbage)]
    [InlineData(CropKind.Flax)]
    [InlineData(CropKind.Rye)]
    public void Bounds_match_actual_wet_and_dry_growth_without_revealing_future_weather(CropKind crop)
    {
        var village=NewVillage();
        village.Structures.Add(new(){Kind=BuildingKind.Farm,PlotId="21,12",BuiltAt=Now});
        FarmRules.EnsureBeds(village);var bed=village.Fields[0];
        bed.Crop=crop;bed.Growth=.25;bed.UpdatedAt=Now;bed.WateredUntil=Now.AddMinutes(30);
        var snapshot=System.Text.Json.JsonSerializer.Serialize(bed);
        var estimate=FarmRules.MaturityWindow(village,bed,Now)!.Value;
        var needed=.75*FarmCatalog.Get(crop).GrowHours;
        Assert.Equal(Now.AddHours(needed),estimate.Earliest);
        Assert.Equal(Now.AddHours(.5+(needed-.5)/FarmCatalog.DryGrowthRate),estimate.Latest);
        Assert.Equal(snapshot,System.Text.Json.JsonSerializer.Serialize(bed));
        SetSky(village,false);
        Assert.Equal(1,FarmRules.GrowthOf(village,bed,estimate.Latest),6);
        Assert.True(FarmRules.GrowthOf(village,bed,estimate.Latest.AddMinutes(-1))<1);
        bed.WateredUntil=estimate.Earliest;
        Assert.Equal(1,FarmRules.GrowthOf(village,bed,estimate.Earliest),6);
    }

    [Fact]
    public void Empty_has_no_forecast_ripe_is_now_and_saved_water_cannot_delay_a_dry_bound()
    {
        var village=NewVillage();var bed=new FieldBed{PlotId="21,12",UpdatedAt=Now};
        Assert.Null(FarmRules.MaturityWindow(village,bed,Now));
        bed.Crop=CropKind.Turnip;bed.Growth=1;
        Assert.Equal((Now,Now),FarmRules.MaturityWindow(village,bed,Now));
        bed.Growth=.75;bed.WateredUntil=Now.AddHours(10);
        Assert.Equal((Now.AddMinutes(15),Now.AddMinutes(15)),FarmRules.MaturityWindow(village,bed,Now));
        bed.WateredUntil=null;
        Assert.Equal(Now.AddHours(1),FarmRules.MaturityWindow(village,bed,Now)!.Value.Latest);
    }

    [Fact]
    public void Forecast_counts_already_elapsed_growth_and_uses_current_conditions_after_care()
    {
        var village=NewVillage();SetSky(village,false);
        var bed=new FieldBed{PlotId="21,12",Crop=CropKind.Turnip,UpdatedAt=Now};
        var at=Now.AddHours(1);
        Assert.Equal((at.AddMinutes(45),at.AddHours(3)),FarmRules.MaturityWindow(village,bed,at));
        bed.WateredUntil=at.AddMinutes(30);
        // Wetness just applied at `at`, with the historical growth checkpoint retained.
        bed.Growth=.25;bed.UpdatedAt=at;
        Assert.Equal((at.AddMinutes(45),at.AddHours(1.5)),FarmRules.MaturityWindow(village,bed,at));
    }
}
