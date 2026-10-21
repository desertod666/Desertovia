using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.World;
using Desertod.Domain.Combat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class RegionalTravelApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static Task<HttpResponseMessage> Go(FoundedVillage village,HttpClient actor,string who,TilePoint spot)=>actor.PostAsJsonAsync(village.Path+"/"+who+"/goto",new{spot});

    [Fact]
    public async Task Regional_travel_cannot_remove_an_active_resident_or_tsar_from_battle()
    {
        var v=await players.FoundVillageAsync("No battle escape");var a=await players.JoinAsync(v,"Defender");
        var battleId=Guid.NewGuid();
        await using(var db=await Db())
        {
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==a.Id);var world=await db.Villages.SingleAsync(w=>w.Id==citizen.VillageId);var now=factory.Clock.GetUtcNow();
            db.RaidBattles.Add(new RaidBattle{Id=battleId,VillageId=world.Id,Sector="west",Phase="Fighting",StartsAt=now,LastTickAt=now,EndsAt=now.AddMinutes(10),
                Units=[new(){X=-10,Y=13,Health=1000000,MaximumHealth=1000000,Attack=0,Speed=0}],
                Fighters=[new(){CitizenId=a.Id,AccountId=citizen.AccountId,Active=true,Health=31,MaximumHealth=40,X=15.5,Y=13.5},
                    new(){IsTsar=true,AccountId=world.OwnerAccountId,Active=true,Health=31,MaximumHealth=40,X=15.5,Y=13.5}]});
            await db.SaveChangesAsync();
        }
        try
        {
            await AssertRuleBrokenAsync(await Go(v,a.Client,"me",RegionalTerrain.MarshTrail),"goto.retreatRequired");
            await AssertRuleBrokenAsync(await Go(v,v.Tsar,"tsar",RegionalTerrain.MarshTrail),"goto.retreatRequired");
            await using var db=await Db();Assert.Null((await db.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip);
            Assert.Null((await db.Villages.SingleAsync(w=>w.Slug==v.Slug)).TsarTrip);
        }
        finally
        {
            await using var db=await Db();
            await db.RaidBattles.Where(b=>b.Id==battleId).ExecuteUpdateAsync(s=>s.SetProperty(b=>b.FinishedAt,factory.Clock.GetUtcNow()));
        }
    }

    [Fact]
    public async Task Downgrade_refuses_to_erase_explored_southern_land_and_leaves_schema_and_fog_intact()
    {
        var v=await players.FoundVillageAsync("Keep discovered land");byte[] fog;
        await players.ArrangeVillageAsync(v.Slug,(world,_)=>Exploration.Reveal(world.Explored,RegionalTerrain.MarshTrail,2));
        await using(var db=await Db())
        {
            fog=(await db.Villages.SingleAsync(w=>w.Slug==v.Slug)).Explored;
            var error=await Assert.ThrowsAnyAsync<Exception>(()=>db.Database.MigrateAsync("20261020225359_AccountNotificationPreferences"));
            Assert.Contains("Southern land has been explored",error.ToString());
        }
        await using var check=await Db();
        Assert.Contains("20261020232201_SouthernRegions",await check.Database.GetAppliedMigrationsAsync());
        Assert.Equal(fog,(await check.Villages.SingleAsync(w=>w.Slug==v.Slug)).Explored);
    }

    [Fact]
    public async Task Persisted_route_survives_context_reload_and_rerouting_starts_at_current_position()
    {
        var v=await players.FoundVillageAsync("South path");var a=await players.JoinAsync(v,"Explorer");
        await OkJsonAsync(await Go(v,a.Client,"me",RegionalTerrain.MarshTrail));
        Trip saved;byte[] oldFog;
        await using(var db=await Db())
        {
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==a.Id);saved=citizen.Trip!;
            Assert.NotNull(saved);Assert.True(saved.Waypoints.Count>2);
            oldFog=(await db.Villages.SingleAsync(w=>w.Slug==v.Slug)).Explored;
            Assert.False(Exploration.IsExplored(oldFog,14,92));
        }
        factory.Clock.Advance(TimeSpan.FromSeconds(10));
        var snapshot=await OkJsonAsync(await a.Client.GetAsync(v.Path));
        var person=snapshot.GetProperty("citizens").EnumerateArray().Single(c=>c.GetProperty("id").GetGuid()==a.Id);
        Assert.Equal(saved.SetOutAt,person.GetProperty("journey").GetProperty("setOutAt").GetDateTimeOffset());
        Assert.Equal(saved.ArrivesAt,person.GetProperty("journey").GetProperty("arrivesAt").GetDateTimeOffset());
        var before=factory.Clock.GetUtcNow();
        await OkJsonAsync(await Go(v,a.Client,"me",WorldMap.Places[WorldMap.Camp]));
        await using var check=await Db();var next=(await check.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip!;
        // PostgreSQL timestamps have microsecond precision; the clock/JSON may retain100ns ticks.
        Assert.True(saved.PositionAt(next.SetOutAt).DistanceTo(next.From)<.0001);Assert.True(next.SetOutAt>=before);
        Assert.False(Exploration.IsExplored((await check.Villages.SingleAsync(w=>w.Slug==v.Slug)).Explored,14,92));
        Assert.Empty(await check.EconomyReceipts.Where(r=>r.AccountId==(check.Citizens.Single(c=>c.Id==a.Id).AccountId)).ToArrayAsync());
    }

    [Fact]
    public async Task Arrival_opens_only_the_destination_region_and_return_is_another_saved_walk()
    {
        var v=await players.FoundVillageAsync("South return");var a=await players.JoinAsync(v,"Explorer");
        await OkJsonAsync(await Go(v,a.Client,"me",RegionalTerrain.MarshTrail));
        await using(var db=await Db())
        {var trip=(await db.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip!;factory.Clock.Advance(trip.ArrivesAt-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));}
        var state=(await OkJsonAsync(await a.Client.GetAsync(v.Path))).GetProperty("state");
        Assert.Equal(JsonValueKind.Object,state.GetProperty("worldRoutes").EnumerateArray().Single(r=>r.GetProperty("id").GetString()=="southern-marsh").GetProperty("destination").ValueKind);
        var fog=Convert.FromBase64String(state.GetProperty("explored").GetString()!);
        Assert.True(Exploration.IsExplored(fog,14,92));Assert.False(Exploration.IsExplored(fog,54,104));
        await OkJsonAsync(await Go(v,a.Client,"me",WorldMap.Places[WorldMap.Camp]));
        await using var check=await Db();var home=(await check.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip!;
        Assert.Equal(RegionalTerrain.MarshTrail,home.From);Assert.True(home.ArrivesAt>factory.Clock.GetUtcNow());
        Assert.Equal(WorldMap.Places[WorldMap.Camp],home.To);
    }

    [Fact]
    public async Task Tsar_and_resident_both_need_the_bridge_and_invalid_destination_preserves_the_current_trip()
    {
        var v=await players.FoundVillageAsync("South bridge");var a=await players.JoinAsync(v,"Explorer");
        foreach(var(actor,who) in new[]{(v.Tsar,"tsar"),(a.Client,"me")})
            await AssertRuleBrokenAsync(await Go(v,actor,who,RegionalTerrain.QuarryTrail),"bridge.closed");
        await players.ArrangeVillageAsync(v.Slug,(world,_)=>world.BridgeOpened=true);
        await OkJsonAsync(await Go(v,v.Tsar,"tsar",RegionalTerrain.QuarryTrail));
        await using var db=await Db();var before=JsonSerializer.Serialize((await db.Villages.SingleAsync(w=>w.Slug==v.Slug)).TsarTrip);
        await AssertRuleBrokenAsync(await Go(v,v.Tsar,"tsar",new(63.5,83.5)),"spot.noPath");
        await using var check=await Db();Assert.Equal(before,JsonSerializer.Serialize((await check.Villages.SingleAsync(w=>w.Slug==v.Slug)).TsarTrip));
    }

    [Fact]
    public async Task Legacy_fog_and_trip_json_without_waypoints_are_upgraded_without_losing_old_discovery()
    {
        var v=await players.FoundVillageAsync("Old map");var a=await players.JoinAsync(v,"Explorer");
        var old=Enumerable.Repeat((byte)0x55,Exploration.LegacySize).ToArray();
        var trip=JsonSerializer.Serialize(new{FromX=5d,FromY=5d,ToX=8d,ToY=5d,SetOutAt=factory.Clock.GetUtcNow(),SpeedMultiplier=1d});
        await using(var db=await Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Villages\" SET \"Explored\"={old} WHERE \"Slug\"={v.Slug}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Citizens\" SET \"Trip\"={trip}::jsonb WHERE \"Id\"={a.Id}");
        }
        await OkJsonAsync(await a.Client.GetAsync(v.Path));
        await using var check=await Db();var world=await check.Villages.SingleAsync(w=>w.Slug==v.Slug);
        Assert.Equal(Exploration.Size,world.Explored.Length);
        for(var i=0;i<old.Length;i++)Assert.Equal(old[i],(byte)(world.Explored[i]&old[i]));
        Assert.Empty((await check.Citizens.SingleAsync(c=>c.Id==a.Id)).Trip!.Waypoints);
    }
}
