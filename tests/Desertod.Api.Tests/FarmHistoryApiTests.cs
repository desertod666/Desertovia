using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Farming;
using Desertod.Domain.Weather;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FarmHistoryApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task<(FoundedVillage V,Resident A,Resident B,Resident C)> Setup()
    {
        var v=await players.FoundVillageAsync("Crop history");var a=await players.JoinAsync(v,"Andrey");var b=await players.JoinAsync(v,"Maria");var c=await players.JoinAsync(v,"Ivan");
        await players.ArrangeVillageAsync(v.Slug,(world,now)=>{
            world.FarmerAdmissionEnabled=true;world.Structures.Add(new(){Kind=BuildingKind.Farm,PlotId="21,12",BuiltAt=now});FarmRules.EnsureBeds(world);
            // This fixture exercises useful manual watering, not the random sky drawn from the village ID.
            // Cover the work/settlement advances too, including a crossing into the next weather period.
            for(var period=DayCycle.PeriodOf(now);period<=DayCycle.PeriodOf(now.AddHours(2));period++)
                world.Forecasts.Add(new(){Day=period,Rain=false,MadeAt=now});
        });
        foreach(var person in new[]{a,b,c})await players.ArrangeCitizenAsync(person.Id,(citizen,_)=>citizen.IsFarmer=true);
        return(v,a,b,c);
    }
    private async Task<FarmWorkTarget> Target(FoundedVillage v)
    {
        await using var db=await Db();var world=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var bed=world.Fields.Single(b=>b.Index==2);
        return new(){PlotId=bed.PlotId,Index=bed.Index,GenerationId=bed.GenerationId};
    }
    private static Task<HttpResponseMessage> Start(FoundedVillage v,Resident a,string task,FarmWorkTarget target,Guid? id=null)=>
        a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId=task,workplacePlotId=target.PlotId,farmTarget=target,requestId=id??Guid.NewGuid()});
    private async Task Finish(FoundedVillage v,Resident a)
    {factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));}
    private async Task<FarmWorkTarget> Plant(FoundedVillage v,Resident a)
    {await OkJsonAsync(await Start(v,a,"sow-turnips",await Target(v)));await Finish(v,a);return await Target(v);}

    [Fact]
    public async Task Shared_crop_keeps_people_through_leave_rest_roundtrip_and_idempotent_harvest()
    {
        var(v,a,b,c)=await Setup();var target=await Plant(v,a);
        await OkJsonAsync(await Start(v,b,"water-fields",target));await Finish(v,b);
        var state=(await OkJsonAsync(await b.Client.GetAsync(v.Path))).GetProperty("state");
        Assert.Equal(2,state.GetProperty("fields").EnumerateArray().Single(x=>x.GetProperty("index").GetInt32()==2).GetProperty("contributions").GetArrayLength());
        var growing=state.GetProperty("fields").EnumerateArray().Single(x=>x.GetProperty("index").GetInt32()==2);
        Assert.True(growing.GetProperty("earliestRipeAt").GetDateTimeOffset()<=growing.GetProperty("latestRipeAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.NoContent,(await a.Client.DeleteAsync(v.Path+"/me")).StatusCode);
        await players.ArrangeVillageAsync(v.Slug,(world,now)=>{var bed=world.Fields.Single(x=>x.Index==2);bed.Growth=1;bed.UpdatedAt=now;});
        var request=Guid.NewGuid();await OkJsonAsync(await Start(v,c,"harvest",target,request));
        await OkJsonAsync(await c.Client.PutAsJsonAsync(v.Path+"/me/rest",new{resting=true}));await Finish(v,c);
        await OkJsonAsync(await Start(v,c,"harvest",target,request));
        await using var db=await Db();var record=await db.FarmCrops.SingleAsync(x=>x.Id==target.GenerationId);
        Assert.Equal(3,record.Contributions.Count);Assert.NotNull(record.HarvestedAt);Assert.NotEmpty(record.Produced);
        Assert.Equal(3,record.Contributions.Select(x=>x.AccountId).Distinct().Count());
        var world=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);Assert.Empty(world.Fields.Single(x=>x.Index==2).Contributions);
        var history=await OkJsonAsync(await b.Client.GetAsync(v.Path+"/farm/history?plotId=21%2C12"));
        var item=Assert.Single(history.EnumerateArray());Assert.Equal("Harvested",item.GetProperty("status").GetString());
        Assert.Equal(3,item.GetProperty("contributions").GetArrayLength());Assert.NotEmpty(item.GetProperty("received").EnumerateArray());
    }

    [Fact]
    public async Task Two_harvesters_cannot_add_two_harvest_contributions_or_two_crop_results()
    {
        var(v,a,b,c)=await Setup();var target=await Plant(v,a);
        await players.ArrangeVillageAsync(v.Slug,(world,now)=>{world.Fields[2].Growth=1;world.Fields[2].UpdatedAt=now;});
        var replies=await Task.WhenAll(Start(v,b,"harvest",target),Start(v,c,"harvest",target));
        Assert.Single(replies,r=>r.IsSuccessStatusCode);await AssertRuleBrokenAsync(replies.Single(r=>!r.IsSuccessStatusCode),"farm.busy");
        await Finish(v,replies[0].IsSuccessStatusCode?b:c);
        await using var db=await Db();var records=await db.FarmCrops.Where(x=>x.Id==target.GenerationId).ToListAsync();
        var record=Assert.Single(records);Assert.Single(record.Contributions,x=>x.Step==FarmStep.Harvest);
        Assert.Equal(2,record.Contributions.Count);
    }

    [Fact]
    public async Task Old_json_without_history_fields_loads_and_finishes_without_inventing_contributors()
    {
        var(v,a,_,_)=await Setup();var target=await Target(v);
        await players.ArrangeVillageAsync(v.Slug,(world,now)=>{world.Fields[2].Crop=CropKind.Turnip;world.Fields[2].Growth=1;world.Fields[2].UpdatedAt=now;});
        await using(var db=await Db())
        {
            var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Villages\" SET \"Fields\" = (SELECT jsonb_agg(item - 'Contributions' - 'ContributionVersion' - 'SownAt') FROM jsonb_array_elements(\"Fields\") item) WHERE \"Id\" = {id}");
        }
        await OkJsonAsync(await a.Client.GetAsync(v.Path));
        await OkJsonAsync(await Start(v,a,"harvest",target));
        // A pre-migration accepted cycle has neither the addressed JSON column nor a history version.
        await using(var legacy=await Db())
            await legacy.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Citizens\" SET \"CycleFarmTargets\" = NULL, \"CycleFarmContributionVersion\" = 0 WHERE \"Id\" = {a.Id}");
        await OkJsonAsync(await a.Client.GetAsync(v.Path));await Finish(v,a);
        await using var check=await Db();Assert.False(await check.FarmCrops.AnyAsync(x=>x.Id==target.GenerationId));
        Assert.Null((await check.Villages.SingleAsync(x=>x.Slug==v.Slug)).Fields[2].Crop);
    }

    [Fact]
    public async Task Removed_bed_history_survives_and_cursor_is_bounded_to_one_world()
    {
        var(v,a,_,_)=await Setup();var target=await Plant(v,a);var other=await players.FoundVillageAsync("Other crop history");
        await players.ArrangeVillageAsync(v.Slug,(world,_)=>world.Fields.Clear());
        await using(var db=await Db())
        {
            var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
            var otherId=await db.Villages.Where(x=>x.Slug==other.Slug).Select(x=>x.Id).SingleAsync();
            for(var i=0;i<20;i++)db.FarmCrops.Add(new(){Id=Guid.CreateVersion7(),VillageId=id,PlotId="21,12",BedIndex=0,Crop=CropKind.Turnip,ContributionVersion=1,SownAt=factory.Clock.GetUtcNow()});
            db.FarmCrops.Add(new(){Id=Guid.CreateVersion7(),VillageId=otherId,PlotId="private-other-world",BedIndex=0,Crop=CropKind.Flax,ContributionVersion=1,SownAt=factory.Clock.GetUtcNow()});
            await db.SaveChangesAsync();
        }
        var first=(await OkJsonAsync(await a.Client.GetAsync(v.Path+"/farm/history"))).EnumerateArray().ToArray();Assert.Equal(20,first.Length);
        var second=(await OkJsonAsync(await a.Client.GetAsync(v.Path+"/farm/history?before="+first[^1].GetProperty("id").GetGuid()))).EnumerateArray().ToArray();Assert.Single(second);
        var all=first.Concat(second).ToArray();Assert.Equal(21,all.Select(x=>x.GetProperty("id").GetGuid()).Distinct().Count());
        var crop=all.Single(x=>x.GetProperty("id").GetGuid()==target.GenerationId);Assert.Equal("Lost",crop.GetProperty("status").GetString());
        Assert.Single(crop.GetProperty("contributions").EnumerateArray());Assert.All(all,x=>Assert.Equal("21,12",x.GetProperty("plotId").GetString()));
    }
}
