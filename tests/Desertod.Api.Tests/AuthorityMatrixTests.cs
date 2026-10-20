using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AuthorityMatrixTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    [Fact]
    public async Task Nickname_and_other_world_ownership_never_grant_authority_and_revocation_applies_to_existing_session()
    {
        var village = await players.FoundVillageAsync("Authority matrix");
        var namesake = await players.JoinAsync(village, "Tsar");
        var governor = await players.JoinAsync(village, "Planner");
        var other = await players.FoundVillageAsync("Other authority");
        var rolePath = village.Path + $"/citizens/{governor.Id}/governor";
        await AssertForbiddenAsync(await namesake.Client.PutAsJsonAsync(rolePath, new { isGovernor = true }));
        await AssertForbiddenAsync(await other.Tsar.PutAsJsonAsync(rolePath, new { isGovernor = true }));
        await OkJsonAsync(await village.Tsar.PutAsJsonAsync(rolePath, new { isGovernor = true }));
        var blueprint = new { kind = "WoodPile", x = 9, y = 11 };
        await AssertForbiddenAsync(await namesake.Client.PostAsJsonAsync(village.Path + "/blueprints", blueprint));
        var request = await OkJsonAsync(await governor.Client.PostAsJsonAsync(village.Path + "/blueprints", blueprint));
        Assert.Empty(request.GetProperty("sites").EnumerateArray());
        var order = Assert.Single(request.GetProperty("civicOrders").EnumerateArray()).GetProperty("id").GetGuid();
        await AssertForbiddenAsync(await governor.Client.PostAsJsonAsync(village.Path + $"/civic/{order}", new { approve = true }));
        await AssertForbiddenAsync(await governor.Client.PutAsJsonAsync(village.Path + "/director/mode", new { mode = "Off" }));
        await OkJsonAsync(await village.Tsar.PutAsJsonAsync(rolePath, new { isGovernor = false }));
        // Same signed-in client, without logout/reload or any client-side permission update.
        await AssertForbiddenAsync(await governor.Client.PostAsJsonAsync(village.Path + "/blueprints", blueprint));
        await AssertForbiddenAsync(await governor.Client.PutAsJsonAsync(village.Path + "/gate", new { open = true }));
        await AssertForbiddenAsync(await governor.Client.PutAsJsonAsync(rolePath, new { isGovernor = true }));
        // Revocation does not erase the historical pending request or turn it into a construction site.
        var current = (await OkJsonAsync(await village.Tsar.GetAsync(village.Path))).GetProperty("state");
        Assert.Empty(current.GetProperty("sites").EnumerateArray());
        Assert.Equal(order, Assert.Single(current.GetProperty("civicOrders").EnumerateArray()).GetProperty("id").GetGuid());
        await OkJsonAsync(await village.Tsar.PostAsJsonAsync(village.Path + $"/civic/{order}", new { approve = true }));
    }

    [Fact]
    public async Task Governor_changes_use_the_same_database_authority_lock_as_commands()
    {
        var village = await players.FoundVillageAsync("Role serialization");
        var resident = await players.JoinAsync(village);
        var factoryDb = factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>();
        await using var db = await factoryDb.CreateDbContextAsync();
        var id = await db.Villages.Where(v => v.Slug == village.Slug).Select(v => v.Id).SingleAsync();
        var actor = await db.Villages.Where(v => v.Id == id).Select(v => v.OwnerAccountId).SingleAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var key = id.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        var command = factory.Services.GetRequiredService<GameService>().SetGovernorAsync(village.Slug, actor, resident.Id, true, default);
        try
        {
            // Observe an actual waiter, rather than relying on an assumed network/scheduler delay.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var waiting = false;
            while (!command.IsCompleted && !waiting)
            {
                waiting = await db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted").SingleAsync(timeout.Token) > 0;
                if (!waiting) await Task.Delay(20, timeout.Token);
            }
            Assert.True(waiting, "Role mutation must wait for the world's transaction lock.");
            Assert.False(command.IsCompleted);
        }
        finally { await transaction.RollbackAsync(); }
        await command;
        Assert.True((await players.LoadCitizenAsync(resident.Id)).IsGovernor);
    }
}
