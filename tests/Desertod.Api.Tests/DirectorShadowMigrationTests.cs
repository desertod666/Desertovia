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

public sealed class DirectorShadowMigrationTests
{
    [Fact]
    public async Task Existing_executed_receipt_stays_executed_and_shadow_history_and_active_mode_refuse_lossy_downgrade()
    {
        PostgreSqlContainer? container=null; string? owned=null,admin=null; string connection;
        var configured=Environment.GetEnvironmentVariable("DESERTOD_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(configured))
        { container=new PostgreSqlBuilder("postgres:17").Build();await container.StartAsync();connection=container.GetConnectionString(); }
        else
        {
            var settings=new NpgsqlConnectionStringBuilder(configured){Database="postgres",Pooling=false};
            Assert.True(settings.Host is "localhost" or "127.0.0.1" or "::1");
            admin=settings.ConnectionString;owned="desertod_shadow_"+Guid.NewGuid().ToString("N");
            await using var server=new NpgsqlConnection(admin);await server.OpenAsync();
            await using var create=new NpgsqlCommand($"CREATE DATABASE \"{owned}\"",server);await create.ExecuteNonQueryAsync();
            settings.Database=owned;connection=settings.ConnectionString;
        }
        try
        {
            await using var db=new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();var now=DateTimeOffset.UtcNow;var day=DateOnly.FromDateTime(now.UtcDateTime);
            var owner=new Account{DisplayName="Shadow migration fixture",CreatedAt=now};db.Accounts.Add(owner);
            var world=Village.Create("shadow-migration","Shadow migration",owner.Id,now);db.Villages.Add(world);
            db.KingdomEconomies.Add(new(){VillageId=world.Id,TreasuryCoins=89,StartedAt=now});
            db.GameWallets.Add(new(){VillageId=world.Id,AccountId=owner.Id,Coins=123});
            db.DirectorFinancePolicies.Add(new(){VillageId=world.Id,Version=1,RequestId=Guid.NewGuid(),OwnerAccountId=owner.Id,At=now});
            db.DirectorFinanceDays.Add(new(){VillageId=world.Id,Day=day,Actions=1,Reserved=10,Revision=1});
            db.AlmightyStates.Add(new(){VillageId=world.Id,Mode="Off"});
            var original=Guid.NewGuid();db.AlmightyObservations.Add(new(){VillageId=world.Id,Id=original,At=now});
            db.DirectorFinanceActions.Add(new(){VillageId=world.Id,ReviewId=original,PolicyVersion=1,Day=day,
                DelegatingOwnerId=owner.Id,Option="finance_work_low",At=now,Applied=true,Result="Synthetic historical receipt"});
            db.LedgerEntries.Add(new(){VillageId=world.Id,DirectorReviewId=original,OperationId=original,
                Operation="work-reserve",Asset="DZK",Location="treasury",Delta=-10,At=now});
            await db.SaveChangesAsync();
            const string previous="20261021131534_DirectorFinance";
            await db.GetService<IMigrator>().MigrateAsync(previous);db.ChangeTracker.Clear();
            async Task<string[]> Rows()=>await db.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(a)::text AS "Value" FROM "Accounts" a
                UNION ALL SELECT to_jsonb(v)::text FROM "Villages" v
                UNION ALL SELECT to_jsonb(w)::text FROM "GameWallets" w
                UNION ALL SELECT to_jsonb(e)::text FROM "KingdomEconomies" e
                UNION ALL SELECT to_jsonb(l)::text FROM "LedgerEntries" l
                UNION ALL SELECT to_jsonb(p)::text FROM "DirectorFinancePolicies" p
                UNION ALL SELECT to_jsonb(d)::text FROM "DirectorFinanceDays" d
                UNION ALL SELECT to_jsonb(o)::text FROM "AlmightyObservations" o
                UNION ALL SELECT (to_jsonb(a)-'Applied')::text FROM "DirectorFinanceActions" a ORDER BY "Value"
                """).ToArrayAsync();
            var before=await Rows();await db.Database.MigrateAsync();Assert.Equal(before,await Rows());
            Assert.True((await db.DirectorFinanceActions.AsNoTracking().SingleAsync(x=>x.ReviewId==original)).Applied);
            Assert.False(db.Database.HasPendingModelChanges());
            var state=await db.AlmightyStates.SingleAsync(x=>x.VillageId==world.Id);state.Mode="ShadowEconomy";await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            state.Mode="Off";await db.SaveChangesAsync();
            var shadow=Guid.NewGuid();db.AlmightyObservations.Add(new(){VillageId=world.Id,Id=shadow,At=now});
            db.DirectorFinanceActions.Add(new(){VillageId=world.Id,ReviewId=shadow,PolicyVersion=1,Day=day,
                DelegatingOwnerId=owner.Id,Option="finance_issue",Applied=false,At=now});await db.SaveChangesAsync();
            Assert.False((await db.DirectorFinanceActions.AsNoTracking().SingleAsync(x=>x.ReviewId==shadow)).Applied);
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.True((await db.DirectorFinanceActions.AsNoTracking().SingleAsync(x=>x.ReviewId==original)).Applied);
            Assert.Equal(89,(await db.KingdomEconomies.AsNoTracking().SingleAsync()).TreasuryCoins);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Villages\" WHERE \"Id\"={world.Id}");
            await db.GetService<IMigrator>().MigrateAsync(previous);await db.Database.MigrateAsync();Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            if(owned is not null)
            {
                Assert.StartsWith("desertod_shadow_",owned);await using var server=new NpgsqlConnection(admin);await server.OpenAsync();
                await using var drop=new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)",server);await drop.ExecuteNonQueryAsync();
            }
            if(container is not null)await container.DisposeAsync();
        }
    }
}
