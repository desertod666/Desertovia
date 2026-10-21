using System.Net.Http.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class ToolDurabilityCompatibilityTests
{
    [Fact]
    public async Task Live_0716_json_survives_upgrade_and_worn_stacks_survive_reload()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync();
        await using var db = new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261018205150_InGameReports");
        var now = DateTimeOffset.UtcNow;
        var account = new Account { DisplayName = "Legacy tool owner", CreatedAt = now };
        db.Accounts.Add(account);
        var village = Village.Create("legacy-tools", "Legacy", account.Id, now);
        var storage = StorageBuilding.Create(StorageKind.TownHall, "13,5", now);
        storage.Put(ResourceKind.Sword, 2, 3); village.Storages.Add(storage);
        var citizen = CharacterCreation.Create(village.Id, account.Id, "Resident", now);
        citizen.Carry(ResourceKind.Arrows, 4, 2); citizen.Renown = 73;
        citizen.CurrentTaskId = "forage"; citizen.CycleSettled = false; citizen.CycleEndsAt = now.AddMinutes(2);
        db.Villages.Add(village); db.Citizens.Add(citizen); await db.SaveChangesAsync();
        // An actual 0.7.16 JSON writer did not know the new property.
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Citizens" SET "Load"=(SELECT jsonb_agg(g-'Durability') FROM jsonb_array_elements("Load") g);
            UPDATE "Villages" SET "Storages"=(SELECT jsonb_agg(jsonb_set(s,ARRAY['Goods'],
              COALESCE((SELECT jsonb_agg(g-'Durability') FROM jsonb_array_elements(s->'Goods') g),'[]'::jsonb)))
              FROM jsonb_array_elements("Storages") s);
            """);
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.False(db.Database.HasPendingModelChanges());
        var restored = await db.Citizens.SingleAsync();
        var city = await db.Villages.SingleAsync();
        Assert.Equal(73, restored.Renown); Assert.Equal("forage", restored.CurrentTaskId);
        Assert.False(restored.CycleSettled); Assert.Equal(4, restored.Carried(ResourceKind.Arrows));
        Assert.Equal(2, restored.Load[0].Sharpen); Assert.Equal(0, restored.Load[0].Durability);
        Assert.Equal(3, city.Storages[0].Goods[0].Sharpen);
        restored.Carry(ResourceKind.StoneAxe, 1, durability: 2);
        restored.Carry(ResourceKind.StoneAxe, 1);
        StorageRules.Unload(restored, city);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        restored = await db.Citizens.SingleAsync(); city = await db.Villages.SingleAsync();
        StorageRules.Take(restored, city, "13,5", ResourceKind.StoneAxe, 1, now.AddMinutes(3), durability: 2);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(2, Assert.Single((await db.Citizens.SingleAsync()).Load).Durability);
        var goods = (await db.Villages.SingleAsync()).Storages[0].Goods;
        Assert.Equal(8, goods.Single(g => g.Kind == ResourceKind.StoneAxe).Durability);
        Assert.Equal(2, goods.Single(g => g.Kind == ResourceKind.Sword).Amount);
    }
}
