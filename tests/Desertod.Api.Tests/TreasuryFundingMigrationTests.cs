using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class TreasuryFundingMigrationTests
{
    [Fact]
    public async Task Existing_money_audits_survive_and_new_declared_funding_cannot_be_erased_or_referenced_across_worlds()
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
            Assert.True(settings.Host is "localhost" or "127.0.0.1" or "::1", "Only an explicit loopback test cluster is allowed.");
            admin = settings.ConnectionString; owned = "desertod_funding_" + Guid.NewGuid().ToString("N");
            await using var administrator = new NpgsqlConnection(admin); await administrator.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{owned}\"", administrator); await create.ExecuteNonQueryAsync();
            settings.Database = owned; connection = settings.ConnectionString;
        }
        try
        {
            await using var db = new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync(); var now = DateTimeOffset.UtcNow;
            var owner = new Account { DisplayName = "Funding migration fixture", CreatedAt = now }; db.Accounts.Add(owner);
            var world = Village.Create("funding-migration", "Funding migration", owner.Id, now); db.Villages.Add(world);
            db.GameWallets.Add(new() { VillageId = world.Id, AccountId = owner.Id, Coins = 123 });
            db.KingdomEconomies.Add(new() { VillageId = world.Id, StartedAt = now, TreasuryCoins = 89 });
            var batch = new MonetaryBatch { VillageId = world.Id, Id = Guid.NewGuid(), At = now, Finalized = true,
                BeforeTotal = 202, AfterTotal = 212, Issued = 10, PostingCount = 1, BeforeHash = "before", AfterHash = "after", PostingHash = "posting" };
            db.MonetaryBatches.Add(batch);
            db.LedgerEntries.Add(new() { VillageId = world.Id, FinancialBatchId = batch.Id, AccountId = owner.Id,
                OperationId = Guid.NewGuid(), Operation = "wage", Asset = "DZK", Location = "wallet", Delta = 10, At = now });
            await db.SaveChangesAsync();
            const string previous = "20261021095643_MonetaryConservation";
            await db.GetService<IMigrator>().MigrateAsync(previous); db.ChangeTracker.Clear();
            async Task<string[]> Rows() => await db.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(v)::text AS "Value" FROM "Villages" v
                UNION ALL SELECT to_jsonb(a)::text FROM "Accounts" a
                UNION ALL SELECT to_jsonb(w)::text FROM "GameWallets" w
                UNION ALL SELECT to_jsonb(e)::text FROM "KingdomEconomies" e
                UNION ALL SELECT to_jsonb(l)::text FROM "LedgerEntries" l
                UNION ALL SELECT to_jsonb(b)::text FROM "MonetaryBatches" b ORDER BY "Value"
                """).ToArrayAsync();
            var before = await Rows(); await db.Database.MigrateAsync(); Assert.Equal(before, await Rows()); Assert.False(db.Database.HasPendingModelChanges());
            Assert.Empty(await db.TreasuryFundingPolicies.ToListAsync()); Assert.Empty(await db.TreasuryIssueDays.ToListAsync());
            var policy = new TreasuryFundingPolicy { VillageId = world.Id, Version = 1, RequestId = Guid.NewGuid(), ActorId = owner.Id,
                PublishedAt = now, EffectiveAt = now, Enabled = true, DailyIssueLimit = 100 };
            db.TreasuryFundingPolicies.Add(policy); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            var day = DateOnly.FromDateTime(now.UtcDateTime); db.TreasuryIssueDays.Add(new() { VillageId = world.Id, Day = day, Issued = 10, Revision = 1 });
            db.TreasuryIssues.Add(new() { VillageId = world.Id, RequestId = Guid.NewGuid(), ActorId = owner.Id, PolicyVersion = 1,
                Day = day, FinancialBatchId = batch.Id, Amount = 10, IssuedTotalAfter = 10, RemainingAtIssue = 90, At = now });
            await db.SaveChangesAsync(); await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            var other = Village.Create("other-fund", "Other fund", owner.Id, now); db.Villages.Add(other); await db.SaveChangesAsync();
            db.TreasuryIssues.Add(new() { VillageId = other.Id, RequestId = Guid.NewGuid(), PolicyVersion = 1, Day = day, FinancialBatchId = batch.Id, Amount = 1, At = now });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Villages\" WHERE \"Id\"={world.Id}");
            Assert.Empty(await db.TreasuryIssues.ToListAsync()); Assert.Empty(await db.TreasuryIssueDays.ToListAsync()); Assert.Empty(await db.TreasuryFundingPolicies.ToListAsync());
            Assert.Empty(await db.MonetaryBatches.ToListAsync()); Assert.Empty(await db.LedgerEntries.ToListAsync());
            await db.GetService<IMigrator>().MigrateAsync(previous); await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            if (owned is not null)
            {
                Assert.StartsWith("desertod_funding_", owned);
                await using var administrator = new NpgsqlConnection(admin); await administrator.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)", administrator); await drop.ExecuteNonQueryAsync();
            }
            if (container is not null) await container.DisposeAsync();
        }
    }
}
