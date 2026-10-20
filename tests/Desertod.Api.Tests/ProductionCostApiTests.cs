using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ProductionCostApiTests(DesertodApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private readonly Players players = new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Sale_uses_recorded_cost_once_and_unknown_history_is_not_profit(bool known)
    {
        var village = await players.FoundVillageAsync("Recorded trade cost");
        var seller = await players.JoinAsync(village, "Seller"); var buyer = await players.JoinAsync(village, "Buyer");
        await seller.Client.GetAsync(village.Path + "/economy"); await buyer.Client.GetAsync(village.Path + "/economy");
        await using (var db = await Db())
        {
            var a = await db.Citizens.SingleAsync(c => c.Id == seller.Id); var b = await db.Citizens.SingleAsync(c => c.Id == buyer.Id);
            PersonalInventory.Add(a, ResourceKind.Wood, 4, 40, known);
            (await db.GameWallets.SingleAsync(w => w.VillageId == b.VillageId && w.AccountId == b.AccountId)).Coins = 1000;
            await db.SaveChangesAsync();
        }
        var id = Guid.NewGuid();
        await OkJsonAsync(await seller.Client.PostAsJsonAsync(village.Path + "/economy", new {requestId = id, action = "list", resource = "Wood", amount = 2, price = 70}));
        var buy = new {requestId = Guid.NewGuid(), action = "buy-lot", target = id};
        foreach (var response in await Task.WhenAll(buyer.Client.PostAsJsonAsync(village.Path + "/economy", buy), buyer.Client.PostAsJsonAsync(village.Path + "/economy", buy))) await OkJsonAsync(response);
        var view = await seller.Client.GetFromJsonAsync<EconomyView>(village.Path + "/economy", JsonOptions);
        Assert.Equal(70, view!.Coins); Assert.Equal(known ? 50 : 0, view.ProfitToday);
        Assert.Equal(20, Assert.Single(view.Inventory).CostBasis); Assert.Equal(known, view.Inventory[0].CostBasisKnown);
        var bought = await buyer.Client.GetFromJsonAsync<EconomyView>(village.Path + "/economy", JsonOptions);
        Assert.Equal(930, bought!.Coins); Assert.Equal(70, Assert.Single(bought.Inventory).CostBasis); Assert.True(bought.Inventory[0].CostBasisKnown);
    }

    [Fact]
    public async Task Old_JSON_without_cost_marker_stays_unknown_through_transfer_listing_and_cancel()
    {
        var village = await players.FoundVillageAsync("Legacy unknown cost"); var sender = await players.JoinAsync(village); var recipient = await players.JoinAsync(village);
        Guid target;
        await using (var db = await Db())
        {
            target = await db.Citizens.Where(c => c.Id == recipient.Id).Select(c => c.AccountId).SingleAsync();
            var legacy = "[{\"Kind\":\"Wood\",\"Amount\":4,\"CostBasis\":41}]";
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Citizens\" SET \"PersonalGoods\" = CAST({legacy} AS jsonb) WHERE \"Id\" = {sender.Id}");
        }
        var before = await sender.Client.GetFromJsonAsync<EconomyView>(village.Path + "/economy", JsonOptions);
        Assert.False(Assert.Single(before!.Inventory).CostBasisKnown);
        await OkJsonAsync(await sender.Client.PostAsJsonAsync(village.Path + "/economy", new {requestId = Guid.NewGuid(), action = "transfer", resource = "Wood", amount = 2, target}));
        var lot = Guid.NewGuid();
        await OkJsonAsync(await recipient.Client.PostAsJsonAsync(village.Path + "/economy", new {requestId = lot, action = "list", resource = "Wood", amount = 2, price = 90}));
        await OkJsonAsync(await recipient.Client.PostAsJsonAsync(village.Path + "/economy", new {requestId = Guid.NewGuid(), action = "cancel-lot", target = lot}));
        var after = await recipient.Client.GetFromJsonAsync<EconomyView>(village.Path + "/economy", JsonOptions);
        Assert.False(Assert.Single(after!.Inventory).CostBasisKnown); Assert.Equal(20, after.Inventory[0].CostBasis);
        Assert.Equal(41, (await players.LoadCitizenAsync(sender.Id)).PersonalGoods.Sum(g => g.CostBasis) + (await players.LoadCitizenAsync(recipient.Id)).PersonalGoods.Sum(g => g.CostBasis));
    }

    [Fact]
    public async Task Persisted_private_craft_keeps_input_cost_when_it_finishes_into_safe_rest()
    {
        var village = await players.FoundVillageAsync("Private craft cost"); var resident = await players.JoinAsync(village);
        await using (var db = await Db())
        {
            var c = await db.Citizens.SingleAsync(c => c.Id == resident.Id); var v = await db.Villages.SingleAsync(v => v.Id == c.VillageId);
            c.Stats.Set(Stat.Dexterity, 4); c.Stats.Set(Stat.Strength, 2);
            PersonalInventory.Add(c, ResourceKind.Wood, 7, 101);
            v.Structures.Add(new() {Kind = BuildingKind.Workshop, PlotId = "-4,10", OwnerAccountId = c.AccountId, BuiltAt = factory.Clock.GetUtcNow()});
            v.Research.Add(new() {TechnologyId = TechnologyCatalog.Carpentry, ResearchedAt = factory.Clock.GetUtcNow()});
            await db.SaveChangesAsync();
        }
        var started = await OkJsonAsync(await resident.Client.PostAsJsonAsync(village.Path + "/me/task", new {taskId = "saw-planks", workplacePlotId = "-4,10"}));
        Assert.Equal(28, (await players.LoadCitizenAsync(resident.Id)).CycleInputCostBasis);
        await OkJsonAsync(await resident.Client.PutAsJsonAsync(village.Path + "/me/rest", new {resting = true}));
        factory.Clock.Advance(started.GetProperty("cycleEndsAt").GetDateTimeOffset() - factory.Clock.GetUtcNow() + TimeSpan.FromSeconds(1));
        var settled = await resident.Client.GetFromJsonAsync<JsonElement>(village.Path + "/me");
        Assert.Equal("Resting", settled.GetProperty("restState").GetString());
        var view = await resident.Client.GetFromJsonAsync<EconomyView>(village.Path + "/economy", JsonOptions);
        var planks = Assert.Single(view!.Inventory, g => g.Kind == ResourceKind.Planks);
        Assert.True(planks.CostBasisKnown); Assert.Equal(28, planks.CostBasis);
        Assert.Equal(101, view.Inventory.Sum(g => g.CostBasis)); Assert.Equal(0, view.Coins);
    }
}
