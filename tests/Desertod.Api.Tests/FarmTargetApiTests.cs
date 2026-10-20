using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Farming;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FarmTargetApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task<(FoundedVillage V,Resident A,Resident B)> Setup()
    {
        var v=await players.FoundVillageAsync("Addressed farm");var a=await players.JoinAsync(v,"Farmer");var b=await players.JoinAsync(v,"Helper");
        await players.ArrangeVillageAsync(v.Slug,(village,now)=>{village.Structures.Add(new(){Kind=BuildingKind.Farm,PlotId="21,12",BuiltAt=now});FarmRules.EnsureBeds(village);});
        return(v,a,b);
    }
    private async Task<FarmWorkTarget> Target(string slug,int index)
    {
        await using var db=await Db();var v=await db.Villages.SingleAsync(v=>v.Slug==slug);var bed=v.Fields.Single(b=>b.Index==index);
        return new(){PlotId=bed.PlotId,Index=bed.Index,GenerationId=bed.GenerationId};
    }
    private static Task<HttpResponseMessage> Sow(Resident a,FarmWorkTarget target,Guid? requestId=null)=>a.Client.PostAsJsonAsync(a.Path+"/me/task",new{taskId="sow-turnips",workplacePlotId=target.PlotId,farmTarget=target,requestId=requestId??Guid.NewGuid()});

    [Fact]
    public async Task Bed_target_and_generation_persist_before_rest_and_only_that_bed_is_sown()
    {
        var(v,a,_)=await Setup();
        var target=await Target(v.Slug,2);var id=Guid.NewGuid();
        await OkJsonAsync(await Sow(a,target,id));
        var pending=await players.LoadCitizenAsync(a.Id);Assert.Equal(target.GenerationId,Assert.Single(pending.CycleFarmTargets).GenerationId);
        Assert.Equal(2,pending.CycleFarmTargets[0].Index);
        await OkJsonAsync(await a.Client.PutAsJsonAsync(v.Path+"/me/rest",new{resting=true}));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        await OkJsonAsync(await Sow(a,target,id)); // Retry returns the accepted operation, never sows another bed.
        await using var db=await Db();var saved=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        Assert.Equal(CropKind.Turnip,saved.Fields.Single(b=>b.Index==2).Crop);Assert.Null(saved.Fields.Single(b=>b.Index==0).Crop);
        Assert.NotEqual(target.GenerationId,saved.Fields.Single(b=>b.Index==2).GenerationId);
    }

    [Fact]
    public async Task Concurrent_sow_cannot_reserve_one_bed_twice_and_cancellation_frees_it()
    {
        var(v,a,b)=await Setup();var target=await Target(v.Slug,0);var first=Guid.NewGuid();var second=Guid.NewGuid();
        var replies=await Task.WhenAll(Sow(a,target,first),Sow(b,target,second));
        Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.BadRequest);
        await AssertRuleBrokenAsync(replies.Single(r=>r.StatusCode==HttpStatusCode.BadRequest),"farm.busy");
        var winner=replies[0].IsSuccessStatusCode?a:b;var loser=winner==a?b:a;var retry=winner==a?second:first;
        await OkJsonAsync(await winner.Client.DeleteAsync(v.Path+"/me/task"));
        await OkJsonAsync(await Sow(loser,target,retry));
        Assert.Single((await players.LoadCitizenAsync(loser.Id)).CycleFarmTargets);
    }

    [Fact]
    public async Task Missing_legacy_generation_loads_without_replacing_crop_and_stale_target_spends_nothing()
    {
        var(v,a,_)=await Setup();
        await players.ArrangeVillageAsync(v.Slug,(village,now)=>{var bed=village.Fields[0];bed.Crop=CropKind.Turnip;bed.Growth=.25;bed.UpdatedAt=now;});
        await using(var db=await Db())
        {
            var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Villages\" SET \"Fields\" = (SELECT jsonb_agg(item - 'GenerationId') FROM jsonb_array_elements(\"Fields\") item) WHERE \"Id\" = {id}");
        }
        using var response=await a.Client.GetAsync(v.Path);var state=(await OkJsonAsync(response)).GetProperty("state");
        var fields=state.GetProperty("fields").EnumerateArray().ToArray();Assert.Equal("Turnip",fields[0].GetProperty("crop").GetString());
        Assert.Equal(Guid.Empty,fields[0].GetProperty("generationId").GetGuid());
        var before=await players.LoadCitizenAsync(a.Id);var stale=new FarmWorkTarget{PlotId="21,12",Index=2,GenerationId=Guid.NewGuid()};
        await AssertRuleBrokenAsync(await Sow(a,stale),"farm.stale");
        var after=await players.LoadCitizenAsync(a.Id);Assert.Equal(before.StoredEnergy,after.StoredEnergy);Assert.Null(after.CurrentTaskId);
    }

    [Fact]
    public async Task Pre_farm_start_receipt_remains_retryable_after_optional_target_was_added()
    {
        var v=await players.FoundVillageAsync("Start receipt compatibility");var a=await players.JoinAsync(v);
        var request=new Desertod.Api.Game.StartTaskRequest("chop-wood",RequestId:Guid.NewGuid());
        var legacy=new{request.TaskId,request.Spot,request.WorkplacePlotId,request.Ownership,request.WorkOrderId,request.RequestId};
        var fingerprint="start:"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(legacy)));
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==a.Id);
            db.EconomyReceipts.Add(new(){VillageId=c.VillageId,AccountId=c.AccountId,RequestId=request.RequestId!.Value,Fingerprint=fingerprint,At=factory.Clock.GetUtcNow()});await db.SaveChangesAsync();
        }
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",request));
        Assert.Null((await players.LoadCitizenAsync(a.Id)).CurrentTaskId);
    }
}
