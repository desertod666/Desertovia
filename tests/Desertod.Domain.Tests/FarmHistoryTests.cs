using System.Text.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Rules;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class FarmHistoryTests
{
    private static Village Farm()
    {
        var v=NewVillage();v.FarmerAdmissionEnabled=true;v.Structures.Add(new(){Kind=BuildingKind.Farm,PlotId="21,12",BuiltAt=Now});FarmRules.EnsureBeds(v);return v;
    }
    private static Citizen Farmer(Village v,string name){var c=NewCitizen(v,name,Now);c.IsFarmer=true;return c;}
    private static FarmWorkTarget Target(FieldBed b)=>new(){PlotId=b.PlotId,Index=b.Index,GenerationId=b.GenerationId};
    private static CycleReport Sow(Village v,Citizen c,int index=2)
    {CycleEngine.Start(c,Task("sow-turnips"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[index]));return Finish(c,WorkContext.For(v));}

    [Fact]
    public void Different_people_keep_one_crop_history_and_harvest_snapshot_survives_bed_clearing()
    {
        var v=Farm();var a=Farmer(v,"Andrey");var b=Farmer(v,"Maria");var c=Farmer(v,"Ivan");
        var planted=Assert.Single(Sow(v,a).FarmChanges);var bed=v.Fields[2];var generation=bed.GenerationId;
        CycleEngine.Start(b,Task("water-fields"),WorkContext.For(v),a.CycleEndsAt!.Value.AddSeconds(1),farmTarget:Target(bed));
        var watered=Assert.Single(Finish(b,WorkContext.For(v)).FarmChanges);
        bed.Growth=1;bed.UpdatedAt=b.CycleEndsAt!.Value;
        CycleEngine.Start(c,Task("harvest"),WorkContext.For(v),b.CycleEndsAt.Value.AddSeconds(1),farmTarget:Target(bed));
        var harvest=Assert.Single(Finish(c,WorkContext.For(v)).FarmChanges);
        Assert.Equal(generation,harvest.Id);Assert.Equal(3,harvest.Contributions.Count);
        Assert.Equal(new[]{"Andrey","Maria","Ivan"},harvest.Contributions.Select(x=>x.Name));
        Assert.Equal(new[]{FarmStep.Sow,FarmStep.Water,FarmStep.Harvest},harvest.Contributions.Select(x=>x.Step));
        Assert.NotEmpty(harvest.Produced);Assert.Empty(harvest.Lost);Assert.NotNull(harvest.HarvestedAt);
        Assert.Null(bed.Crop);Assert.Empty(bed.Contributions);Assert.NotEqual(generation,bed.GenerationId);
        Assert.Single(planted.Contributions);Assert.Equal(2,watered.Contributions.Count);
        Assert.Null(CycleEngine.Settle(c,WorkContext.For(v),c.CycleEndsAt!.Value.AddHours(1)));
    }

    [Fact]
    public void Appointment_removal_and_serialization_do_not_remove_already_accepted_contribution()
    {
        var v=Farm();var c=Farmer(v,"Andrey");CycleEngine.Start(c,Task("sow-turnips"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[2]));
        c.IsFarmer=false;c=JsonSerializer.Deserialize<Citizen>(JsonSerializer.Serialize(c))!;
        v=JsonSerializer.Deserialize<Village>(JsonSerializer.Serialize(v))!;
        var report=Finish(c,WorkContext.For(v));
        Assert.Equal(c.AccountId,Assert.Single(Assert.Single(report.FarmChanges).Contributions).AccountId);
        var reloaded=JsonSerializer.Deserialize<Village>(JsonSerializer.Serialize(v))!;
        Assert.Equal("Andrey",Assert.Single(reloaded.Fields[2].Contributions).Name);
    }

    [Fact]
    public void Rain_or_a_repeated_wet_bed_produces_no_extra_contribution()
    {
        var v=Farm();var a=Farmer(v,"Andrey");Sow(v,a);var b=Farmer(v,"Maria");
        CycleEngine.Start(b,Task("water-fields"),WorkContext.For(v),a.CycleEndsAt!.Value.AddSeconds(1),farmTarget:Target(v.Fields[2]));
        SetSky(v,true);var report=Finish(b,WorkContext.For(v));
        Assert.Empty(report.FarmChanges);Assert.Equal(0,report.PrimaryExperience);Assert.Single(v.Fields[2].Contributions);
        SetSky(v,false);FarmRules.Water(v,b.CycleEndsAt!.Value,targets:[Target(v.Fields[2])]);
        var before=v.Fields[2].Contributions.Count;
        Assert.Throws<Desertod.Domain.GameRuleException>(()=>CycleEngine.Start(Farmer(v,"Ivan"),Task("water-fields"),WorkContext.For(v),b.CycleEndsAt.Value.AddSeconds(1),farmTarget:Target(v.Fields[2])));
        Assert.Equal(before,v.Fields[2].Contributions.Count);
    }

    [Fact]
    public void New_crop_growth_starts_at_accepted_completion_even_when_settlement_is_two_hours_late()
    {
        var v=Farm();var c=Farmer(v,"Andrey");CycleEngine.Start(c,Task("sow-turnips"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[2]));
        var due=c.CycleEndsAt!.Value;var later=due.AddHours(2);
        var snapshot=Assert.Single(CycleEngine.Settle(c,WorkContext.For(v),later)!.FarmChanges);
        Assert.Equal(due,snapshot.SownAt);Assert.Equal(due,Assert.Single(snapshot.Contributions).CompletedAt);
        Assert.Equal(.5,FarmRules.GrowthOf(v,v.Fields[2],later),6);
    }

    [Fact]
    public void Enrollment_does_not_rewrite_previously_accepted_sow_or_its_growth_timestamp()
    {
        var v=Farm();v.FarmerAdmissionEnabled=false;var c=Farmer(v,"Andrey");
        CycleEngine.Start(c,Task("sow-turnips"),WorkContext.For(v),Now,farmTarget:Target(v.Fields[2]));
        v.FarmerAdmissionEnabled=true;var later=c.CycleEndsAt!.Value.AddHours(2);
        var report=CycleEngine.Settle(c,WorkContext.For(v),later)!;
        Assert.Empty(report.FarmChanges);Assert.Equal(0,v.Fields[2].ContributionVersion);Assert.Equal(later,v.Fields[2].UpdatedAt);
    }

    [Fact]
    public void Late_bulk_water_keeps_legacy_timing_but_does_not_give_new_crop_fresh_water_on_login()
    {
        var v=Farm();var a=Farmer(v,"Andrey");Sow(v,a);
        FarmRules.Sow(v,CropKind.Turnip,a.CycleEndsAt!.Value,target:Target(v.Fields[0]));
        var b=Farmer(v,"Maria");CycleEngine.Start(b,Task("water-fields"),WorkContext.For(v),a.CycleEndsAt.Value.AddSeconds(1));
        var due=b.CycleEndsAt!.Value;var later=due.AddHours(1);
        var report=CycleEngine.Settle(b,WorkContext.For(v),later)!;
        Assert.Single(report.FarmChanges);Assert.Equal(due+FarmCatalog.WaterDuration,v.Fields[2].WateredUntil);
        Assert.Equal(later+FarmCatalog.WaterDuration,v.Fields[0].WateredUntil);Assert.Empty(v.Fields[0].Contributions);
    }

    [Fact]
    public void Legacy_private_farm_never_creates_public_contribution_history()
    {
        var v=Farm();var c=Farmer(v,"Andrey");v.Structures.Single().OwnerAccountId=c.AccountId;
        Assert.Empty(Sow(v,c).FarmChanges);Assert.Empty(v.Fields[2].Contributions);Assert.Equal(0,v.Fields[2].ContributionVersion);
    }

    [Fact]
    public void Missing_or_null_legacy_collection_reads_as_unknown_empty_history()
    {
        var bed=JsonSerializer.Deserialize<FieldBed>("""{"PlotId":"21,12","Index":2,"Crop":null,"Contributions":null}""")!;
        Assert.Empty(bed.Contributions);Assert.Equal(0,bed.ContributionVersion);Assert.Null(bed.SownAt);
    }
}
