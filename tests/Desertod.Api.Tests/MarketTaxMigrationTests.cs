using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class MarketTaxMigrationTests
{
    [Fact]
    public async Task Old_wallets_lots_and_history_survive_and_reverse_migration_refuses_fiscal_state()
    {
        PostgreSqlContainer? container=null;string? owned=null,admin=null;string connection;
        var configured=Environment.GetEnvironmentVariable("DESERTOD_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(configured))
        {
            container=new PostgreSqlBuilder("postgres:17").Build();await container.StartAsync();connection=container.GetConnectionString();
        }
        else
        {
            var settings=new NpgsqlConnectionStringBuilder(configured){Database="postgres",Pooling=false};
            Assert.True(settings.Host is "localhost" or "127.0.0.1" or "::1","Only an explicit loopback test cluster is allowed.");
            admin=settings.ConnectionString;owned="desertod_market_tax_"+Guid.NewGuid().ToString("N");
            await using var administrator=new NpgsqlConnection(admin);await administrator.OpenAsync();
            await using var create=new NpgsqlCommand($"CREATE DATABASE \"{owned}\"",administrator);await create.ExecuteNonQueryAsync();
            settings.Database=owned;connection=settings.ConnectionString;
        }
        try
        {
            await using var db=new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();var now=DateTimeOffset.UtcNow;
            var owner=new Account{DisplayName="Fiscal migration fixture",CreatedAt=now};db.Accounts.Add(owner);
            var world=Village.Create("tax-migration","Tax migration",owner.Id,now);db.Villages.Add(world);
            var wallet=new GameWallet{VillageId=world.Id,AccountId=owner.Id,Coins=123,EarnedToday=45,ProfitToday=67};db.GameWallets.Add(wallet);
            var economy=new KingdomEconomy{VillageId=world.Id,StartedAt=now,TreasuryCoins=89};db.KingdomEconomies.Add(economy);
            var lot=new MarketLot{VillageId=world.Id,SellerAccountId=owner.Id,Amount=2,Price=50,CostBasis=17,CostBasisKnown=false,CreatedAt=now,Status="open"};db.MarketLots.Add(lot);
            db.EconomyReceipts.Add(new(){VillageId=world.Id,RequestId=Guid.NewGuid(),AccountId=owner.Id,At=now,Fingerprint="preserved receipt"});await db.SaveChangesAsync();
            const string previous="20261021071117_AlmightyCategoryPolicy";
            await db.GetService<IMigrator>().MigrateAsync(previous);db.ChangeTracker.Clear();
            async Task<string[]> Rows()=>await db.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(w)::text AS "Value" FROM "Villages" w
                UNION ALL SELECT to_jsonb(a)::text FROM "Accounts" a
                UNION ALL SELECT (to_jsonb(w)-'TaxPaidToday'-'TaxableProfitToday'-'TaxRemainder'-'TaxRevision')::text FROM "GameWallets" w
                UNION ALL SELECT (to_jsonb(e)-'LatestTaxPolicyVersion')::text FROM "KingdomEconomies" e
                UNION ALL SELECT (to_jsonb(l)-'TaxPolicyVersion')::text FROM "MarketLots" l
                UNION ALL SELECT to_jsonb(r)::text FROM "EconomyReceipts" r ORDER BY "Value"
                """).ToArrayAsync();
            var before=await Rows();await db.Database.MigrateAsync();Assert.Equal(before,await Rows());Assert.False(db.Database.HasPendingModelChanges());
            wallet=await db.GameWallets.SingleAsync();Assert.Equal(0,wallet.TaxPaidToday);Assert.Equal(0,wallet.TaxableProfitToday);
            Assert.Null((await db.MarketLots.SingleAsync()).TaxPolicyVersion);
            wallet.TaxRemainder=1;await db.SaveChangesAsync();await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            wallet.TaxRemainder=0;economy=await db.KingdomEconomies.SingleAsync();economy.LatestTaxPolicyVersion=1;await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            economy.LatestTaxPolicyVersion=0;
            var policy=new TaxPolicy{VillageId=world.Id,Version=1,RequestId=Guid.NewGuid(),ActorId=owner.Id,PublishedAt=now,EffectiveAt=now};db.TaxPolicies.Add(policy);await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            // Only this throwaway fixture's invented policy is removed to test the empty reverse path.
            db.TaxPolicies.Remove(policy);await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync(previous);await db.Database.MigrateAsync();Assert.Equal(before,await Rows());
            db.ChangeTracker.Clear();
            var quote=new MarketTaxQuote{VillageId=world.Id,Id=Guid.NewGuid(),LotId=lot.Id,SellerAccountId=owner.Id,BuyerAccountId=owner.Id,CreatedAt=now,ExpiresAt=now.AddMinutes(2)};
            db.MarketTaxQuotes.Add(quote);await db.SaveChangesAsync();await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            db.MarketTaxEvents.Add(new(){VillageId=world.Id,Id=Guid.NewGuid(),LotId=lot.Id,QuoteId=quote.Id,SellerAccountId=owner.Id,At=now});await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            // Check PostgreSQL's statement-end NO ACTION semantics across all world cascades.
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Villages\" WHERE \"Id\"={world.Id}");
            Assert.Empty(await db.MarketTaxEvents.ToListAsync());Assert.Empty(await db.MarketTaxQuotes.ToListAsync());
        }
        finally
        {
            if(owned is not null)
            {
                Assert.StartsWith("desertod_market_tax_",owned);
                await using var administrator=new NpgsqlConnection(admin);await administrator.OpenAsync();
                await using var drop=new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)",administrator);await drop.ExecuteNonQueryAsync();
            }
            if(container is not null)await container.DisposeAsync();
        }
    }
}
