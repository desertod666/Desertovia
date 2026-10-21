using System.Text.Json.Nodes;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class RoyalDungeonMigrationTests
{
    [Fact]
    public async Task Migration_preserves_old_bodies_and_checkpoint_then_refuses_to_erase_new_calls_or_frozen_sources()
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
            admin=settings.ConnectionString;owned="desertod_royal_dungeon_"+Guid.NewGuid().ToString("N");
            await using var administrator=new NpgsqlConnection(admin);await administrator.OpenAsync();
            await using var create=new NpgsqlCommand($"CREATE DATABASE \"{owned}\"",administrator);await create.ExecuteNonQueryAsync();
            settings.Database=owned;connection=settings.ConnectionString;
        }
        try
        {
            await using var db=new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();var now=DateTimeOffset.UtcNow;
            var owner=new Account{DisplayName="Migration ruler",CreatedAt=now};db.Accounts.Add(owner);
            var village=Village.Create("royal-dungeon-migration","Migration",owner.Id,now);village.Id=Guid.Parse("11111111-1111-4111-8111-111111111111");village.Trees.Clear();
            village.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));Assert.Equal(1,village.Store(ResourceKind.Spear,1));db.Villages.Add(village);
            var citizen=CharacterCreation.Create(village.Id,owner.Id,"Preserved resident",now);citizen.Renown=317;citizen.Stats.Endurance=6;db.Citizens.Add(citizen);
            var old=new DungeonRun{VillageId=village.Id,DungeonId=DungeonCatalog.DeadLands,Phase=DungeonPhase.Running,OpenedAt=now,
                Heroes=[new(){AccountId=owner.Id,Name="Legacy ruler",Class=HeroClass.Voivode}]};
            var simulation=new DungeonSimulation(DungeonCatalog.Get(old.DungeonId),[new(owner.Id,owner.Id,"Legacy ruler",HeroClass.Voivode,80,8,0,null,false)]);
            simulation.Heroes[0].Health=37;
            var save=new DungeonSave(7,simulation.Capture(),123,[new(owner.Id,null,"Legacy ruler",HeroClass.Voivode,null,80)],[]);
            var json=JsonNode.Parse(save.ToJson())!;
            foreach(var h in json["Fight"]!["Heroes"]!.AsArray())h!["Fighter"]!.AsObject().Remove("CombatClass");
            foreach(var h in json["Heroes"]!.AsArray())h!.AsObject().Remove("CombatClass");
            old.CheckpointJson=json.ToJsonString();db.DungeonRuns.Add(old);await db.SaveChangesAsync();
            const string previous="20261021041138_MortalRoyalCombat";
            await db.GetService<IMigrator>().MigrateAsync(previous);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "DungeonRuns" SET "Heroes"=(SELECT jsonb_agg(h-'CombatClass'-'RoyalBodyCitizenId') FROM jsonb_array_elements("Heroes") h);
                """);
            async Task<string[]> Rows()=>await db.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(v)::text AS "Value" FROM "Villages" v
                UNION ALL SELECT to_jsonb(c)::text FROM "Citizens" c
                UNION ALL SELECT (to_jsonb(r)-'CombatRulesVersion')::text FROM "DungeonRuns" r
                ORDER BY "Value"
                """).ToArrayAsync();
            var before=await Rows();await db.Database.MigrateAsync();Assert.Equal(before,await Rows());Assert.False(db.Database.HasPendingModelChanges());
            var loaded=await db.DungeonRuns.SingleAsync();Assert.Equal(0,loaded.CombatRulesVersion);Assert.Null(loaded.Tsar.CombatClass);
            var restored=DungeonSave.Read(loaded.CheckpointJson!).Resume(loaded,"royal-dungeon-migration");
            Assert.Equal(37,restored.Save!.Fight.Heroes[0].Health);Assert.Equal(80,restored.Fighters[0].Health);Assert.Equal(8,restored.Fighters[0].Damage);Assert.Null(restored.Fighters[0].CombatClass);
            var world=await db.Villages.SingleAsync();var actual=await db.Citizens.SingleAsync();
            var fresh=DungeonRules.Open(world,DungeonCatalog.DeadLands,"Ruler",123,now,[actual]);DungeonRules.PickRoyalClass(fresh,world,[actual],HeroClass.Spearman,now);
            db.DungeonRuns.Add(fresh);await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var roundtrip=await db.DungeonRuns.SingleAsync(r=>r.Id==fresh.Id);Assert.Equal(actual.Id,roundtrip.Tsar.RoyalBodyCitizenId);Assert.Equal(HeroClass.Spearman,roundtrip.Tsar.CombatClass);
            Assert.Equal(317,(await db.Citizens.SingleAsync()).Renown);
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            roundtrip.CombatRulesVersion=0;await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(previous));
            // Delete only the invented new call after returning its real borrowed equipment.
            world=await db.Villages.SingleAsync();actual=await db.Citizens.SingleAsync();roundtrip.CombatRulesVersion=1;
            DungeonRules.Cancel(roundtrip,world,[actual],now);db.DungeonRuns.Remove(roundtrip);await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync(previous);await db.Database.MigrateAsync();db.ChangeTracker.Clear();
            Assert.Equal(37,DungeonSave.Read((await db.DungeonRuns.SingleAsync()).CheckpointJson!).Fight.Heroes[0].Health);
            Assert.Equal(317,(await db.Citizens.SingleAsync()).Renown);Assert.Equal(1,(await db.Villages.SingleAsync()).Stock(ResourceKind.Spear));
        }
        finally
        {
            if(owned is not null)
            {
                Assert.StartsWith("desertod_royal_dungeon_",owned);
                await using var administrator=new NpgsqlConnection(admin);await administrator.OpenAsync();
                await using var drop=new NpgsqlCommand($"DROP DATABASE \"{owned}\" WITH (FORCE)",administrator);await drop.ExecuteNonQueryAsync();
            }
            if(container is not null)await container.DisposeAsync();
        }
    }
}
