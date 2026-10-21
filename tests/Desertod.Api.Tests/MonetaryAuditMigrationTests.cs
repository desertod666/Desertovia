using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class MonetaryAuditMigrationTests
{
    [Fact]
    public async Task Existing_balances_fiscal_history_and_ledger_are_preserved_and_audit_cannot_be_erased()
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
            admin = settings.ConnectionString; owned = "desertod_monetary_" + Guid.NewGuid().ToString("N");
            await using var administrator = new NpgsqlConnection(admin); await administrator.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{owned}\"", administrator); await create.ExecuteNonQueryAsync();
            settings.Database = owned; connection = settings.ConnectionString;
        }
        try
        {
            await using var db = new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync(); var now = DateTimeOffset.UtcNow;
            var owner = new Account { DisplayName = "Money migration fixture", CreatedAt = now }; db.Accounts.Add(owner);
            var world = Village.Create("money-migration", "Money migration", owner.Id, now); db.Villages.Add(world);
            db.GameWallets.Add(new() { VillageId = world.Id, AccountId = owner.Id, Coins = 123, TaxPaidToday = 7, TaxRemainder = 3 });
            db.KingdomEconomies.Add(new() { VillageId = world.Id, StartedAt = now, TreasuryCoins = 89, LatestTaxPolicyVersion = 1 });
            db.TaxPolicies.Add(new() { VillageId = world.Id, Version = 1, RequestId = Guid.NewGuid(), ActorId = owner.Id, PublishedAt = now, EffectiveAt = now });
            var entry = new LedgerEntry { VillageId = world.Id, AccountId = owner.Id, OperationId = Guid.NewGuid(), Operation = "wage", Asset = "DZK", Location = "wallet", Delta = 10, At = now };
            db.LedgerEntries.Add(entry);
            db.EconomyReceipts.Add(new() { VillageId = world.Id, RequestId = Guid.NewGuid(), AccountId = owner.Id, At = now, Fingerprint = "preserved receipt" });
            await db.SaveChangesAsync();
            const string previous = "20261021082020_VersionedSaleTax";
            await db.GetService<IMigrator>().MigrateAsync(previous); db.ChangeTracker.Clear();
            async Task<string[]> Rows() => await db.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(w)::text AS "Value" FROM "Villages" w
                UNION ALL SELECT to_jsonb(a)::text FROM "Accounts" a
                UNION ALL SELECT to_jsonb(w)::text FROM "GameWallets" w
                UNION ALL SELECT to_jsonb(e)::text FROM "KingdomEconomies" e
                UNION ALL SELECT to_jsonb(p)::text FROM "TaxPolicies" p
                UNION ALL SELECT to_jsonb(r)::text FROM "EconomyReceipts" r
                UNION ALL SELECT (to_jsonb(l)-'FinancialBatchId'-'DirectorReviewId')::text FROM "LedgerEntries" l ORDER BY "Value"
                """).ToArrayAsync();
            var before = await Rows(); await db.Database.MigrateAsync(); Assert.Equal(before, await Rows());
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Null((await db.LedgerEntries.SingleAsync()).FinancialBatchId);
            Assert.Null((await db.LedgerEntries.SingleAsync()).DirectorReviewId);
            var batch = new MonetaryBatch { VillageId = world.Id, Id = Guid.NewGuid(), At = now, Finalized = true,
                BeforeTotal = 202, AfterTotal = 212, Issued = 10, PostingCount = 1,
                BeforeHash = "before", AfterHash = "after", PostingHash = "posting",
                Operations = [new() { OperationId = entry.OperationId, Kind = "legacy-wage-issuance", Issued = 10 }] };
            db.MonetaryBatches.Add(batch); (await db.LedgerEntries.SingleAsync()).FinancialBatchId = batch.Id;
            await db.SaveChangesAsync(); await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            // EF commits earlier empty downgrades before reaching this older history guard. Restore current ORM shape.
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear(); Assert.Single(await db.MonetaryBatches.ToListAsync());
            // A world-owned receipt cannot be attached to another world's ledger, even with the same batch ID.
            var other = Village.Create("other-money", "Other money", owner.Id, now); db.Villages.Add(other); await db.SaveChangesAsync();
            db.LedgerEntries.Add(new() { VillageId = other.Id, FinancialBatchId = batch.Id, OperationId = Guid.NewGuid(),
                Operation = "wage", Asset = "DZK", Location = "wallet", AccountId = owner.Id, Delta = 10, At = now });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            // Only invented data in this owned database is removed to exercise the empty reverse path.
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Villages\" WHERE \"Id\"={world.Id}");
            Assert.Empty(await db.MonetaryBatches.ToListAsync()); Assert.Empty(await db.LedgerEntries.ToListAsync());
            await db.GetService<IMigrator>().MigrateAsync(previous); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            if (owned is not null)
            {
                Assert.StartsWith("desertod_monetary_", owned);
                await using var administrator = new NpgsqlConnection(admin); await administrator.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)", administrator); await drop.ExecuteNonQueryAsync();
            }
            if (container is not null) await container.DisposeAsync();
        }
    }
}
