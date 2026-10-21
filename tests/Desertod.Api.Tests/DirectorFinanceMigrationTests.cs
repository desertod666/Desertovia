using Desertod.Domain.Director;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class DirectorFinanceMigrationTests
{
    [Fact]
    public async Task Existing_history_survives_and_delegation_and_action_history_cannot_be_erased_or_cross_world_referenced()
    {
        PostgreSqlContainer? container = null; string? owned = null, admin = null; string connection;
        var configured = Environment.GetEnvironmentVariable("DESERTOD_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured))
        {
            container = new PostgreSqlBuilder("postgres:17").Build(); await container.StartAsync(); connection = container.GetConnectionString();
        }
        else
        {
            var settings = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
            Assert.True(settings.Host is "localhost" or "127.0.0.1" or "::1");
            admin = settings.ConnectionString; owned = "desertod_director_finance_" + Guid.NewGuid().ToString("N");
            await using var administrator = new NpgsqlConnection(admin); await administrator.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{owned}\"", administrator); await create.ExecuteNonQueryAsync();
            settings.Database = owned; connection = settings.ConnectionString;
        }
        try
        {
            await using var db = new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync(); var now = DateTimeOffset.UtcNow;
            var owner = new Account { DisplayName = "Director migration fixture", CreatedAt = now }; db.Accounts.Add(owner);
            var world = Village.Create("director-finance-migration", "Director finance migration", owner.Id, now); db.Villages.Add(world);
            db.GameWallets.Add(new() { VillageId = world.Id, AccountId = owner.Id, Coins = 123 });
            db.KingdomEconomies.Add(new() { VillageId = world.Id, StartedAt = now, TreasuryCoins = 89 });
            db.LedgerEntries.Add(new() { VillageId = world.Id, AccountId = owner.Id, OperationId = Guid.NewGuid(),
                Operation = "wage", Asset = "DZK", Location = "wallet", Delta = 10, At = now });
            db.TreasuryFundingPolicies.Add(new() { VillageId = world.Id, Version = 1, RequestId = Guid.NewGuid(), ActorId = owner.Id,
                PublishedAt = now, EffectiveAt = now, Enabled = true, DailyIssueLimit = 100 });
            await db.SaveChangesAsync();
            const string previous = "20261021103846_TreasuryFunding";
            await db.GetService<IMigrator>().MigrateAsync(previous); db.ChangeTracker.Clear();
            async Task<string[]> Rows() => await db.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(v)::text AS "Value" FROM "Villages" v
                UNION ALL SELECT to_jsonb(a)::text FROM "Accounts" a
                UNION ALL SELECT to_jsonb(w)::text FROM "GameWallets" w
                UNION ALL SELECT to_jsonb(e)::text FROM "KingdomEconomies" e
                UNION ALL SELECT (to_jsonb(l)-'DirectorReviewId')::text FROM "LedgerEntries" l
                UNION ALL SELECT to_jsonb(p)::text FROM "TreasuryFundingPolicies" p ORDER BY "Value"
                """).ToArrayAsync();
            var before = await Rows(); await db.Database.MigrateAsync(); Assert.Equal(before, await Rows());
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.DirectorFinancePolicies.ToArrayAsync());
            Assert.Empty(await db.DirectorFinanceActions.ToArrayAsync());
            Assert.All(await db.LedgerEntries.ToArrayAsync(), x => Assert.Null(x.DirectorReviewId));
            var policy = new DirectorFinancePolicy { VillageId = world.Id, Version = 1, RequestId = Guid.NewGuid(), OwnerAccountId = owner.Id, At = now };
            db.DirectorFinancePolicies.Add(policy); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            var day = DateOnly.FromDateTime(now.UtcDateTime); var review = Guid.NewGuid();
            db.DirectorFinanceDays.Add(new() { VillageId = world.Id, Day = day, Actions = 1, Revision = 1 });
            db.AlmightyObservations.Add(new() { VillageId = world.Id, Id = review, At = now });
            db.DirectorFinanceActions.Add(new() { VillageId = world.Id, ReviewId = review, PolicyVersion = 1, Day = day,
                DelegatingOwnerId = owner.Id, Option = "finance_tax_down", At = now }); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            var other = Village.Create("director-other", "Other director", owner.Id, now); db.Villages.Add(other); await db.SaveChangesAsync();
            db.DirectorFinanceActions.Add(new() { VillageId = other.Id, ReviewId = review, PolicyVersion = 1, Day = day, At = now });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Villages\" WHERE \"Id\"={world.Id}");
            Assert.Empty(await db.DirectorFinanceActions.ToArrayAsync()); Assert.Empty(await db.DirectorFinanceDays.ToArrayAsync());
            Assert.Empty(await db.DirectorFinancePolicies.ToArrayAsync());
            await db.GetService<IMigrator>().MigrateAsync(previous); await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            if (owned is not null)
            {
                Assert.StartsWith("desertod_director_finance_", owned);
                await using var administrator = new NpgsqlConnection(admin); await administrator.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)", administrator); await drop.ExecuteNonQueryAsync();
            }
            if (container is not null) await container.DisposeAsync();
        }
    }
}
