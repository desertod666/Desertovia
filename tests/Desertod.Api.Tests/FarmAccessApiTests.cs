using System.Net.Http.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Farming;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FarmAccessApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private static object Access(bool farmer,Guid? id=null)=>new{requestId=id??Guid.NewGuid(),isFarmer=farmer};
    private static string AccessPath(FoundedVillage v,Resident a)=>v.Path+$"/citizens/{a.Id}/farm-access";
    private static Task<HttpResponseMessage> Enable(FoundedVillage v)=>v.Tsar.PutAsJsonAsync(v.Path+"/farm/admission",new{requestId=Guid.NewGuid()});
    private async Task Farm(FoundedVillage v)=>await players.ArrangeVillageAsync(v.Slug,(world,now)=>{
        world.Structures.Add(new(){Kind=BuildingKind.Farm,PlotId="21,12",BuiltAt=now});FarmRules.EnsureBeds(world);
    });

    [Fact]
    public async Task Only_explicitly_delegated_governor_can_appoint_and_revocation_is_immediate()
    {
        var v=await players.FoundVillageAsync("Farm delegation");var manager=await players.JoinAsync(v,"Farm manager");var a=await players.JoinAsync(v,"Farmer");
        var other=await players.FoundVillageAsync("Other farm authority");
        await AssertForbiddenAsync(await a.Client.PutAsJsonAsync(AccessPath(v,a),Access(true)));
        await AssertForbiddenAsync(await other.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(true)));
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/citizens/{manager.Id}/governor",new{isGovernor=true}));
        await AssertForbiddenAsync(await manager.Client.PutAsJsonAsync(AccessPath(v,a),Access(true)));
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,manager),new{requestId=Guid.NewGuid(),canManageFarm=true}));
        var changed=await OkJsonAsync(await manager.Client.PutAsJsonAsync(AccessPath(v,a),Access(true)));
        Assert.True(changed.GetProperty("isFarmer").GetBoolean());
        await AssertForbiddenAsync(await manager.Client.PutAsJsonAsync(AccessPath(v,manager),new{requestId=Guid.NewGuid(),canManageFarm=true}));
        await AssertForbiddenAsync(await manager.Client.PutAsJsonAsync(v.Path+"/farm/admission",new{requestId=Guid.NewGuid()}));
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/citizens/{manager.Id}/governor",new{isGovernor=false}));
        Assert.False((await players.LoadCitizenAsync(manager.Id)).CanManageFarm);
        await AssertForbiddenAsync(await manager.Client.PutAsJsonAsync(AccessPath(v,a),Access(false)));
        Assert.True((await players.LoadCitizenAsync(a.Id)).IsFarmer);
    }

    [Fact]
    public async Task Public_work_checks_admission_but_accepted_work_finishes_after_removal()
    {
        var v=await players.FoundVillageAsync("Farm admission");var a=await players.JoinAsync(v,"Farmer");await Farm(v);
        await OkJsonAsync(await Enable(v));
        var before=await players.LoadCitizenAsync(a.Id);
        await AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="sow-turnips"}),"farm.admission");
        Assert.Equal(before.StoredEnergy,(await players.LoadCitizenAsync(a.Id)).StoredEnergy);
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(true)));
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="sow-turnips"}));
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(false)));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        var dbFactory=factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>();
        await using var db=await dbFactory.CreateDbContextAsync();
        Assert.Single((await db.Villages.SingleAsync(x=>x.Slug==v.Slug)).Fields,b=>b.Crop is not null);
        await AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="sow-turnips"}),"farm.admission");
    }

    [Fact]
    public async Task Enrollment_preserves_accepted_legacy_work_and_closes_new_abstract_food_work()
    {
        var v=await players.FoundVillageAsync("Farm enrollment");var a=await players.JoinAsync(v);await Farm(v);
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="sow-turnips"}));
        await OkJsonAsync(await Enable(v));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        Assert.True((await players.LoadCitizenAsync(a.Id)).CycleSettled);
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(true)));
        await AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="tend-fields"}),"farm.manualOnly");
    }

    [Fact]
    public async Task Replaying_old_appointment_does_not_undo_later_removal_and_payload_is_bound()
    {
        var v=await players.FoundVillageAsync("Farm access receipts");var a=await players.JoinAsync(v);var id=Guid.NewGuid();
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(true,id)));
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(false)));
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(true,id)));
        Assert.False((await players.LoadCitizenAsync(a.Id)).IsFarmer);
        await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(false,id)),"economy.requestConflict");
        await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),new{requestId=Guid.NewGuid(),canManageFarm=true}),"farm.manager");
        await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(AccessPath(v,a),Access(true,Guid.Empty)),"economy.request");
    }
}
