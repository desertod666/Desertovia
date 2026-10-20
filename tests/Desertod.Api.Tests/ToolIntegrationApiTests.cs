using System.Net.Http.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ToolIntegrationApiTests(DesertodApiFactory factory)
{
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Fact]
    public async Task Count_only_market_never_repairs_worn_tools_or_spends_money_on_missing_fresh_stock()
    {
        var players = new Players(factory);
        var city = await players.FoundVillageAsync("Tool trade compatibility");
        var player = await players.JoinAsync(city, "Tool buyer");
        await Players.OkJsonAsync(await player.Client.GetAsync(city.Path + "/economy"));
        await using (var db = await Db())
        {
            var village = await db.Villages.SingleAsync(v => v.Slug == city.Slug);
            var storage = StorageBuilding.Create(StorageKind.TownHall, "3,3", factory.Clock.GetUtcNow());
            storage.Put(ResourceKind.StoneAxe, 1, durability: 3);
            storage.Put(ResourceKind.StoneAxe, 1);
            village.Storages.Add(storage);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == player.Id);
            var wallet = await db.GameWallets.SingleAsync(w => w.AccountId == citizen.AccountId && w.VillageId == village.Id);
            wallet.Coins = 1000;
            await db.SaveChangesAsync();
        }
        await Players.AssertRuleBrokenAsync(await player.Client.PostAsJsonAsync(city.Path + "/economy",
            new {requestId=Guid.NewGuid(), action="buy-stock", resource="StoneAxe", amount=2}), "inventory.stockMissing");
        var afterRejection = await Players.OkJsonAsync(await player.Client.GetAsync(city.Path + "/economy"));
        Assert.Equal(1000, afterRejection.GetProperty("coins").GetInt64());
        await Players.OkJsonAsync(await player.Client.PostAsJsonAsync(city.Path + "/economy",
            new {requestId=Guid.NewGuid(), action="buy-stock", resource="StoneAxe", amount=1}));
        await Players.OkJsonAsync(await player.Client.PostAsJsonAsync(city.Path + "/economy",
            new {requestId=Guid.NewGuid(), action="donate-stock", resource="StoneAxe", amount=1}));
        await using var check = await Db();
        var kept = await check.Villages.SingleAsync(v => v.Slug == city.Slug);
        var tools = kept.Storages.SelectMany(s => s.Goods).Where(g => g.Kind == ResourceKind.StoneAxe).OrderBy(g => g.Durability).ToArray();
        Assert.Equal(2, tools.Length);
        Assert.Equal((3,1), (tools[0].Durability, tools[0].Amount));
        Assert.Equal((8,1), (tools[1].Durability, tools[1].Amount));
    }

    [Fact]
    public async Task Legacy_json_without_wear_keeps_progress_and_mixed_tool_wear_survives_reload_and_exact_take()
    {
        var players = new Players(factory);
        var city = await players.FoundVillageAsync("Tool JSON compatibility");
        var player = await players.JoinAsync(city, "Existing resident");
        await players.ArrangeCitizenAsync(player.Id, (citizen, _) =>
        {
            citizen.Renown = 73;
            citizen.Stats.StrengthExperience = 41;
            citizen.Carry(ResourceKind.Arrows, 4, 2);
        });
        await players.ArrangeVillageAsync(city.Slug, (village, now) =>
        {
            var storage = StorageBuilding.Create(StorageKind.TownHall, "3,3", now);
            storage.Put(ResourceKind.Sword, 2, 3);
            village.Storages.Add(storage);
        });
        await using (var db = await Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Citizens" SET "Load"=(SELECT jsonb_agg(g-'Durability') FROM jsonb_array_elements("Load") g)
                WHERE "Id"={player.Id};
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Villages" SET "Storages"=(SELECT jsonb_agg(jsonb_set(s,ARRAY['Goods'],
                  COALESCE((SELECT jsonb_agg(g-'Durability') FROM jsonb_array_elements(s->'Goods') g),'[]'::jsonb)))
                  FROM jsonb_array_elements("Storages") s) WHERE "Slug"={city.Slug};
                """);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == player.Id);
            Assert.Equal(73, citizen.Renown);
            Assert.Equal(41, citizen.Stats.StrengthExperience);
            Assert.Equal((4,2,0), (citizen.Load.Single().Amount, citizen.Load.Single().Sharpen, citizen.Load.Single().Durability));
            var village = await db.Villages.SingleAsync(v => v.Slug == city.Slug);
            var sword = village.Storages.SelectMany(s => s.Goods).Single(g => g.Kind == ResourceKind.Sword);
            Assert.Equal((2,3,0), (sword.Amount, sword.Sharpen, sword.Durability));
            var storage = village.Storages.Single(s => s.PlotId == "3,3");
            storage.Put(ResourceKind.StoneAxe, 1, durability: 3);
            storage.Put(ResourceKind.StoneAxe, 1);
            await db.SaveChangesAsync();
        }
        await Players.OkJsonAsync(await player.Client.PostAsJsonAsync(city.Path + "/me/take",
            new {plotId="3,3", resource="StoneAxe", amount=1, durability=3}));
        var restored = await players.LoadCitizenAsync(player.Id);
        Assert.Equal(3, restored.Load.Single(g => g.Kind == ResourceKind.StoneAxe).Durability);
        Assert.Equal(73, restored.Renown);
        await using var check = await Db();
        var remaining = (await check.Villages.SingleAsync(v => v.Slug == city.Slug)).Storages
            .SelectMany(s => s.Goods).Single(g => g.Kind == ResourceKind.StoneAxe);
        Assert.Equal((1,8), (remaining.Amount, remaining.Durability));
        Assert.False(check.Database.HasPendingModelChanges());
    }
}
