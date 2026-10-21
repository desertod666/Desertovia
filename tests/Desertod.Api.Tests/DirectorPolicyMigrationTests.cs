using Desertod.Domain.Director;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class DirectorPolicyMigrationTests
{
    [Fact]
    public async Task Old_history_and_live_lease_survive_upgrade_and_downgrade_refuses_stops_revisions_or_receipts()
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
            Assert.True(settings.Host is "localhost" or "127.0.0.1" or "::1","Migration rehearsals require an explicit loopback test cluster.");
            admin=settings.ConnectionString;owned="desertod_director_policy_"+Guid.NewGuid().ToString("N");
            await using var administrator=new NpgsqlConnection(admin);await administrator.OpenAsync();
            await using var create=new NpgsqlCommand($"CREATE DATABASE \"{owned}\"",administrator);await create.ExecuteNonQueryAsync();
            settings.Database=owned;connection=settings.ConnectionString;
        }
        try
        {
            await using var db=new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();var now=DateTimeOffset.UtcNow;
            var owner=new Account{DisplayName="Director migration ruler",CreatedAt=now};db.Accounts.Add(owner);
            var world=Village.Create("director-policy-migration","Migration",owner.Id,now);db.Villages.Add(world);
            var state=new AlmightyState{VillageId=world.Id,Version=17,Intent="Preserved intent",Lease=Guid.NewGuid(),LeaseUntil=now.AddMinutes(1),CallsToday=3,LastSnapshot="{\"legacy\":true}"};
            db.AlmightyStates.Add(state);
            var observation=new AlmightyObservation{Id=state.Lease.Value,VillageId=world.Id,At=now,Snapshot="{\"options\":[]}",Reason="Saved observation"};
            db.AlmightyObservations.Add(observation);
            db.AlmightyProposals.Add(new(){VillageId=world.Id,ReviewId=Guid.NewGuid(),Option="economy_report",CreatedAt=now,ExpiresAt=now.AddMinutes(30),Reason="Pending history"});
            db.AlmightyProposals.Add(new(){VillageId=world.Id,ReviewId=Guid.NewGuid(),Option="rest",Status="Applied",CreatedAt=now,ExpiresAt=now.AddMinutes(30),Result="Accepted history"});
            await db.SaveChangesAsync();
            const string previous="20261021053133_DungeonRoyalBodies";
            await db.GetService<IMigrator>().MigrateAsync(previous);db.ChangeTracker.Clear();
            async Task<string[]> Rows()=>await db.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(w)::text AS "Value" FROM "Villages" w
                UNION ALL SELECT to_jsonb(a)::text FROM "Accounts" a
                UNION ALL SELECT (to_jsonb(s)-'PolicyVersion'-'DisabledKinds')::text FROM "AlmightyStates" s
                UNION ALL SELECT to_jsonb(o)::text FROM "AlmightyObservations" o
                UNION ALL SELECT to_jsonb(p)::text FROM "AlmightyProposals" p
                ORDER BY "Value"
                """).ToArrayAsync();
            var before=await Rows();await db.Database.MigrateAsync();Assert.Equal(before,await Rows());
            Assert.False(db.Database.HasPendingModelChanges());
            state=await db.AlmightyStates.SingleAsync();Assert.Empty(state.DisabledKinds);Assert.Equal(0,state.PolicyVersion);
            Assert.Equal(observation.Id,state.Lease);Assert.Equal(17,state.Version);Assert.Equal(3,state.CallsToday);
            state.DisabledKinds=["raid"];await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            state.DisabledKinds=[];state.PolicyVersion=1;await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            state.PolicyVersion=0;
            var receipt=new AlmightyPolicyChange{VillageId=world.Id,RequestId=Guid.NewGuid(),ActorId=owner.Id,Kind="raid",Enabled=true,At=now,Changed=false};
            db.AlmightyPolicyChanges.Add(receipt);await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Single(await db.AlmightyPolicyChanges.AsNoTracking().ToArrayAsync());Assert.Equal(before,await Rows());
            // Only invented rehearsal acknowledgements are removed to exercise the empty-state reverse path.
            db.AlmightyPolicyChanges.Remove(receipt);await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync(previous);await db.Database.MigrateAsync();Assert.Equal(before,await Rows());
        }
        finally
        {
            if(owned is not null)
            {
                Assert.StartsWith("desertod_director_policy_",owned);
                await using var administrator=new NpgsqlConnection(admin);await administrator.OpenAsync();
                await using var drop=new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)",administrator);await drop.ExecuteNonQueryAsync();
            }
            if(container is not null)await container.DisposeAsync();
        }
    }
}
