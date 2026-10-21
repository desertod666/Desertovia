using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class MortalRoyalMigrationTests
{
    [Fact]
    public async Task Migration_preserves_old_rows_health_and_json_then_retains_new_body_progress_and_refuses_lossy_rollback()
    {
        PostgreSqlContainer? container=null;string? owned=null,admin=null;
        var configured=Environment.GetEnvironmentVariable("DESERTOD_TEST_POSTGRES");string connection;
        if(string.IsNullOrWhiteSpace(configured))
        {
            container=new PostgreSqlBuilder("postgres:17").Build();await container.StartAsync();connection=container.GetConnectionString();
        }
        else
        {
            var settings=new NpgsqlConnectionStringBuilder(configured){Database="postgres",Pooling=false};
            Assert.True(settings.Host is "localhost" or "127.0.0.1" or "::1","Migration tests require an explicit loopback test cluster.");
            admin=settings.ConnectionString;owned="desertod_mortal_migration_"+Guid.NewGuid().ToString("N");
            await using var dbAdmin=new NpgsqlConnection(admin);await dbAdmin.OpenAsync();
            await using var create=new NpgsqlCommand($"CREATE DATABASE \"{owned}\"",dbAdmin);await create.ExecuteNonQueryAsync();
            settings.Database=owned;connection=settings.ConnectionString;
        }
        try
        {
            var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options;
            await using var db=new DesertodDbContext(options);await db.Database.MigrateAsync();
            var now=DateTimeOffset.UtcNow;var owner=new Account{DisplayName="Migration owner",CreatedAt=now};db.Accounts.Add(owner);
            var village=Village.Create("mortal-migration","Mortal migration",owner.Id,now);db.Villages.Add(village);
            var citizen=CharacterCreation.Create(village.Id,owner.Id,"Existing profile",now);citizen.Renown=317;citizen.Stats.Endurance=6;db.Citizens.Add(citizen);
            var battle=new RaidBattle{Id=Guid.CreateVersion7(),VillageId=village.Id,Sector="west",Phase="Fighting",StartsAt=now,LastTickAt=now,EndsAt=now.AddMinutes(5),
                Fighters=[new(){IsTsar=true,AccountId=owner.Id,Health=37,MaximumHealth=80,X=15.5,Y=13.5,DamageDealt=19,Active=true,NextAttackAt=now.AddSeconds(2)}]};
            db.RaidBattles.Add(battle);await db.SaveChangesAsync();
            const string previous="20261021011454_WorkEncounters";
            await db.GetService<IMigrator>().MigrateAsync(previous);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "RaidBattles" SET "Fighters"=(SELECT jsonb_agg(f-'CombatRulesVersion') FROM jsonb_array_elements("Fighters") f);
                """);
            async Task<string[]> Rows()=>await db.Database.SqlQueryRaw<string>("""
                SELECT (to_jsonb(v)-'RoyalCombat')::text AS "Value" FROM "Villages" v
                UNION ALL SELECT (to_jsonb(b)-'CombatRulesVersion')::text FROM "RaidBattles" b
                UNION ALL SELECT to_jsonb(c)::text FROM "Citizens" c
                ORDER BY "Value"
                """).ToArrayAsync();
            var before=await Rows();await db.Database.MigrateAsync();Assert.Equal(before,await Rows());
            Assert.False(db.Database.HasPendingModelChanges());
            var city=await db.Villages.SingleAsync();var saved=await db.RaidBattles.SingleAsync();var old=Assert.Single(saved.Fighters);
            Assert.Null(city.RoyalCombat);Assert.Equal(0,saved.CombatRulesVersion);Assert.Null(old.CombatRulesVersion);
            Assert.Equal(37,old.Health);Assert.Equal(80,old.MaximumHealth);Assert.Equal(19,old.DamageDealt);
            city.RoyalCombat=new(){Stats=new(){Endurance=9,Strength=4,StrengthExperience=7},Weapon=ResourceKind.Bow,Arrows=1,ArrowSharpen=3,
                Load=[new(){Kind=ResourceKind.WoodenAxe,Amount=1,Durability=4,Sharpen=0}]};
            await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var profile=(await db.Villages.SingleAsync()).RoyalCombat!;
            Assert.Equal(7,profile.Stats.StrengthExperience);Assert.Equal(1,profile.Arrows);Assert.Equal(3,profile.ArrowSharpen);Assert.Equal(4,Assert.Single(profile.Load).Durability);
            Assert.Equal(317,(await db.Citizens.SingleAsync()).Renown);
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            db.ChangeTracker.Clear();Assert.NotNull((await db.Villages.SingleAsync()).RoyalCombat);
            Assert.Contains("20261021041138_MortalRoyalCombat",await db.Database.GetAppliedMigrationsAsync());
            var guardCity=await db.Villages.SingleAsync();var guardBattle=await db.RaidBattles.SingleAsync();
            // Remove only this test's invented profile to exercise the independent
            // accepted-battle guard, even when its body is a real resident instead.
            guardCity.RoyalCombat=null;guardBattle.CombatRulesVersion=1;await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            guardBattle.CombatRulesVersion=0;await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync(previous);await db.Database.MigrateAsync();db.ChangeTracker.Clear();
            Assert.Equal(37,(await db.RaidBattles.SingleAsync()).Fighters.Single().Health);
        }
        finally
        {
            if(owned is not null)
            {
                Assert.StartsWith("desertod_mortal_migration_",owned);
                await using var dbAdmin=new NpgsqlConnection(admin);await dbAdmin.OpenAsync();
                await using var drop=new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)",dbAdmin);await drop.ExecuteNonQueryAsync();
            }
            if(container is not null)await container.DisposeAsync();
        }
    }
}
