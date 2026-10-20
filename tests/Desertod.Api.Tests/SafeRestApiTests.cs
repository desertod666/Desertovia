using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SafeRestApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Fact]
    public async Task Encounter_admitted_after_rest_request_cannot_be_skipped_by_finishing_the_work()
    {
        var village = await players.FoundVillageAsync("Rest and later combat"); var resident = await players.JoinAsync(village);
        var started = await OkJsonAsync(await resident.Client.PostAsJsonAsync(village.Path + "/me/task", new {taskId = "chop-wood"}));
        await OkJsonAsync(await resident.Client.PutAsJsonAsync(village.Path + "/me/rest", new {resting = true}));
        var battleId = Guid.NewGuid();
        await using (var db = await Db())
        {
            var c = await db.Citizens.SingleAsync(c => c.Id == resident.Id); var now = factory.Clock.GetUtcNow();
            db.RaidBattles.Add(new() {Id = battleId, VillageId = c.VillageId, Phase = "Fighting", Sector = "west", StartsAt = now, LastTickAt = now, EndsAt = now.AddMinutes(10),
                Fighters = [new() {CitizenId = c.Id, Active = true, Health = 31, MaximumHealth = 40, X = 15.5, Y = 13.5}]});
            await db.SaveChangesAsync();
        }
        factory.Clock.Advance(started.GetProperty("cycleEndsAt").GetDateTimeOffset() - factory.Clock.GetUtcNow() + TimeSpan.FromSeconds(1));
        var after = await resident.Client.GetFromJsonAsync<JsonElement>(village.Path + "/me");
        Assert.Equal("RestRequested", after.GetProperty("restState").GetString());
        Assert.True((await players.LoadCitizenAsync(resident.Id)).CycleSettled);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await resident.Client.PutAsJsonAsync(village.Path + $"/battles/{battleId}/order", new {mode = "retreat"})).StatusCode);
        await AssertRuleBrokenAsync(await resident.Client.PutAsJsonAsync(village.Path + "/me/rest", new {resting = true}), "rest.retreatRequired");
        await using (var db = await Db())
        {
            var battle = await db.RaidBattles.SingleAsync(b => b.Id == battleId); var f = battle.Fighters.Single();
            Assert.True(f.Active); Assert.Equal(31, f.Health);
            f.DestinationX = f.X; f.DestinationY = f.Y;
            BattleActions.StepRetreat(f, new RaidNavigation(await db.Villages.SingleAsync(v => v.Slug == village.Slug)));
            await db.SaveChangesAsync();
        }
        await OkJsonAsync(await resident.Client.PutAsJsonAsync(village.Path + "/me/rest", new {resting = true}));
        Assert.Equal(RestState.Resting, (await players.LoadCitizenAsync(resident.Id)).RestState);
    }

    [Fact]
    public async Task Accepted_work_survives_rest_request_and_reload_and_is_paid_and_settled_once()
    {
        var village = await players.FoundVillageAsync("Safe rest work");
        var resident = await players.JoinAsync(village);
        var path = village.Path + "/me";
        var started = await OkJsonAsync(await resident.Client.PostAsJsonAsync(path + "/task", new {taskId = "chop-wood"}));
        var end = started.GetProperty("cycleEndsAt").GetDateTimeOffset();
        await OkJsonAsync(await resident.Client.PutAsJsonAsync(path + "/rest", new {resting = true}));
        var requested = await resident.Client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("RestRequested", requested.GetProperty("restState").GetString());
        var requestedAt = requested.GetProperty("restRequestedAt").GetDateTimeOffset();
        foreach (var response in await Task.WhenAll(resident.Client.PutAsJsonAsync(path + "/rest", new {resting = true}), resident.Client.PutAsJsonAsync(path + "/rest", new {resting = true})))
            await OkJsonAsync(response);
        await AssertRuleBrokenAsync(await resident.Client.PostAsJsonAsync(path + "/task", new {taskId = "chop-wood"}), "rest.activeRequired");
        await AssertRuleBrokenAsync(await resident.Client.DeleteAsync(path + "/task"), "rest.activeRequired");
        factory.Clock.Advance(end - factory.Clock.GetUtcNow() + TimeSpan.FromSeconds(1));
        // A different service instance and fresh DB contexts represent a worker after a restart.
        var worker = ActivatorUtilities.CreateInstance<GameService>(factory.Services);
        Guid villageId;
        await using (var db = await Db()) villageId = await db.Villages.Where(v => v.Slug == village.Slug).Select(v => v.Id).SingleAsync();
        await Task.WhenAll(worker.SettleVillageAsync(villageId, [resident.Id], default), factory.Services.GetRequiredService<GameService>().SettleVillageAsync(villageId, [resident.Id], default));
        var saved = await players.LoadCitizenAsync(resident.Id);
        Assert.Equal(RestState.Resting, saved.RestState);
        Assert.Equal(requestedAt, saved.RestRequestedAt);
        Assert.True(saved.CycleSettled);
        var xp = saved.Stats.GetExperience(Stat.Strength);
        Assert.True(xp > 0);
        var wallet = await resident.Client.GetFromJsonAsync<EconomyView>(village.Path + "/economy");
        Assert.True(wallet!.Coins > 0);
        var renown = saved.Renown;
        factory.Clock.Advance(TimeSpan.FromDays(2));
        await worker.SettleVillageAsync(villageId, [resident.Id], default);
        var after = await players.LoadCitizenAsync(resident.Id);
        Assert.Equal(xp, after.Stats.GetExperience(Stat.Strength)); Assert.Equal(renown, after.Renown);
        var later = await resident.Client.GetFromJsonAsync<EconomyView>(village.Path + "/economy");
        Assert.Equal(wallet.Coins, later!.Coins);
        await AssertRuleBrokenAsync(await resident.Client.PostAsJsonAsync(path + "/goto", new {spot = new {x = 15, y = 15}}), "rest.activeRequired");
        await OkJsonAsync(await resident.Client.PutAsJsonAsync(path + "/rest", new {resting = false}));
        await OkJsonAsync(await resident.Client.PostAsJsonAsync(path + "/task", new {taskId = "chop-wood"}));
    }

    [Fact]
    public async Task Rest_requires_completed_retreat_and_never_grants_midfight_immunity()
    {
        var village = await players.FoundVillageAsync("Safe rest battle"); var resident = await players.JoinAsync(village);
        var battleId = Guid.NewGuid();
        await using (var db = await Db())
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == resident.Id);
            var now = factory.Clock.GetUtcNow();
            db.RaidBattles.Add(new RaidBattle {Id = battleId, VillageId = citizen.VillageId, Sector = "west", Phase = "Fighting", StartsAt = now, LastTickAt = now, EndsAt = now.AddMinutes(5),
                Fighters = [new() {CitizenId = citizen.Id, Health = 40, MaximumHealth = 40, Active = true, X = 15.5, Y = 13.5}]});
            await db.SaveChangesAsync();
        }
        await AssertRuleBrokenAsync(await resident.Client.PutAsJsonAsync(village.Path + "/me/rest", new {resting = true}), "rest.retreatRequired");
        Assert.Equal(RestState.Active, (await players.LoadCitizenAsync(resident.Id)).RestState);
        await using (var db = await Db())
        {
            var battle = await db.RaidBattles.SingleAsync(b => b.Id == battleId); var f = battle.Fighters.Single();
            f.Mode = "retreating"; f.DestinationX = f.X; f.DestinationY = f.Y;
            BattleActions.StepRetreat(f, new RaidNavigation(await db.Villages.SingleAsync(v => v.Slug == village.Slug)));
            Assert.False(f.Active); Assert.Equal("withdrawn", f.Mode); await db.SaveChangesAsync();
        }
        await OkJsonAsync(await resident.Client.PutAsJsonAsync(village.Path + "/me/rest", new {resting = true}));
        await AssertRuleBrokenAsync(await resident.Client.PutAsJsonAsync(village.Path + $"/battles/{battleId}/order", new {mode = "intercept"}), "rest.activeRequired");
        await OkJsonAsync(await village.Tsar.PutAsJsonAsync(village.Path + "/me/rest", new {resting = true}));
        var state = (await OkJsonAsync(await village.Tsar.GetAsync(village.Path))).GetProperty("state");
        Assert.Equal("Resting", state.GetProperty("tsar").GetProperty("restState").GetString());
        await AssertRuleBrokenAsync(await village.Tsar.PutAsJsonAsync(village.Path + $"/battles/{battleId}/order", new {mode = "intercept"}), "rest.activeRequired");
    }
}
