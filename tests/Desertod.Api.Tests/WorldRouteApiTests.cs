using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class WorldRouteApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Fact]
    public async Task Atlas_projects_shared_exploration_without_rewriting_land_buildings_or_people()
    {
        var v=await players.FoundVillageAsync("Atlas");var a=await players.JoinAsync(v,"Explorer");
        await players.ArrangeVillageAsync(v.Slug,(world,_)=>{world.Explored=Enumerable.Repeat(byte.MaxValue,Exploration.Size).ToArray();world.Wilds=new(){CaveX=-20,CaveY=-24,LookoutX=-12,LookoutY=-28};});
        string fields;byte[] fog;Guid[] citizens;
        await using(var db=await Db()){
            var world=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);fields=JsonSerializer.Serialize(new{world.Fields,world.Structures,world.Storages});fog=(byte[])world.Explored.Clone();
            citizens=await db.Citizens.Where(c=>c.VillageId==world.Id).Select(c=>c.Id).ToArrayAsync();
        }
        foreach(var client in new[]{v.Tsar,a.Client}){
            var state=(await OkJsonAsync(await client.GetAsync(v.Path))).GetProperty("state");
            var routes=state.GetProperty("worldRoutes");Assert.Equal(5,routes.GetArrayLength());
            Assert.Equal(-19.5,routes.EnumerateArray().Single(r=>r.GetProperty("id").GetString()=="ridge-cave").GetProperty("destination").GetProperty("x").GetDouble());
            Assert.Equal(Convert.ToBase64String(fog),state.GetProperty("explored").GetString());
        }
        await using(var db=await Db()){
            var world=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
            Assert.Equal(fog,world.Explored);Assert.Equal(fields,JsonSerializer.Serialize(new{world.Fields,world.Structures,world.Storages}));
            Assert.Equal(citizens,await db.Citizens.Where(c=>c.VillageId==world.Id).Select(c=>c.Id).ToArrayAsync());
        }
    }

    [Fact]
    public async Task Known_far_bank_can_be_viewed_but_atlas_does_not_authorize_crossing_a_closed_bridge()
    {
        var v=await players.FoundVillageAsync("Atlas bridge");var a=await players.JoinAsync(v,"Explorer");
        await players.ArrangeVillageAsync(v.Slug,(world,_)=>{world.Explored=Enumerable.Repeat(byte.MaxValue,Exploration.Size).ToArray();world.BridgeOpened=false;});
        var routes=(await OkJsonAsync(await a.Client.GetAsync(v.Path))).GetProperty("state").GetProperty("worldRoutes");
        var gold=routes.EnumerateArray().Single(r=>r.GetProperty("id").GetString()=="gold-vein");
        Assert.Equal("bridge",gold.GetProperty("blockedBy").GetString());
        await AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/goto",new{spot=new{x=40,y=17}}),"bridge.closed");
    }
}
