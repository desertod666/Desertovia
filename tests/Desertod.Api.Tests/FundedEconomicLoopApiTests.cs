using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FundedEconomicLoopApiTests(DesertodApiFactory factory)
{
    [Fact]
    public async Task Zero_money_workers_earn_from_a_bounded_fund_then_trade_known_cost_goods_and_return_tax_without_double_wages()
    {
        // Synthetic scenario values only. This does not select rates/limits for the working game.
        using var frozen = factory.Clock.Freeze(); var players = new Players(factory);
        var morning = new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        factory.Clock.Advance(morning - factory.Clock.GetUtcNow());
        var world = await players.FoundVillageAsync("Funded economic loop");
        // Existing communal infrastructure is the scenario precondition; all sale stock must be produced below.
        await players.ArrangeVillageAsync(world.Slug, (v, now) => v.Storages.Add(StorageBuilding.Create(StorageKind.TownHall, "13,5", now)));
        var seller = await players.JoinAsync(world, "Producer"); var buyer = await players.JoinAsync(world, "Purchaser");
        async Task<JsonElement> Economy(HttpClient client, EconomyRequest request) =>
            await Players.OkJsonAsync(await client.PostAsJsonAsync(world.Path + "/economy", request));
        await Economy(world.Tsar, new(Guid.NewGuid(), "enable-work-contracts"));
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding",
            new TreasuryFundingPolicyRequest(Guid.NewGuid(), 0, true, factory.Clock.GetUtcNow(), 300)));
        var state = (await world.Tsar.GetFromJsonAsync<TreasuryFundingStateDto>(world.Path + "/economy/funding"))!;
        var issue = new TreasuryIssueRequest(Guid.NewGuid(), 1, state.BudgetDay, 0, 300);
        var issueReceipt = await Players.OkJsonAsync(await world.Tsar.PostAsJsonAsync(world.Path + "/economy/treasury-issue", issue));
        var order = await Economy(world.Tsar, new(Guid.NewGuid(), "publish-work-order", TaskId: "chop-wood", Cycles: 2, Pay: 100,
            ExpiresAt: factory.Clock.GetUtcNow().AddHours(3)));
        var orderId = Assert.Single(order.GetProperty("workOrders").EnumerateArray()).GetProperty("id").GetGuid();
        foreach (var worker in new[] { seller, buyer })
        {
            await Players.OkJsonAsync(await worker.Client.PostAsJsonAsync(world.Path + "/me/task", new { taskId = "chop-wood", workOrderId = orderId, requestId = Guid.NewGuid() }));
            await Players.OkJsonAsync(await worker.Client.PutAsJsonAsync(world.Path + "/me/rest", new { resting = true }));
        }
        factory.Clock.Advance(TimeSpan.FromHours(2));
        foreach (var worker in new[] { seller, buyer })
        {
            await Players.OkJsonAsync(await worker.Client.GetAsync(world.Path + "/me"));
            var wallet = await Players.OkJsonAsync(await worker.Client.GetAsync(world.Path + "/economy"));
            Assert.Equal(100, wallet.GetProperty("coins").GetInt64()); Assert.Equal(0, wallet.GetProperty("taxPaidToday").GetInt64());
        }
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/tax-policy",
            new TaxPolicyRequest(Guid.NewGuid(), 0, true, factory.Clock.GetUtcNow(), [new() { FromProfit = 0, RateBasisPoints = 1000 }])));
        var cost = EconomyRules.StorePrice(ResourceKind.Wood);
        await Economy(seller.Client, new(Guid.NewGuid(), "buy-stock", ResourceKind.Wood, 1));
        var lot = Guid.NewGuid(); await Economy(seller.Client, new(lot, "list", ResourceKind.Wood, 1, cost + 20));
        var quote = await Players.OkJsonAsync(await buyer.Client.PostAsJsonAsync(world.Path + "/economy/market-quote", new MarketTaxQuoteRequest(Guid.NewGuid(), lot)));
        var purchase = new EconomyRequest(Guid.NewGuid(), "buy-lot", Target: lot, TaxQuoteId: quote.GetProperty("id").GetGuid());
        await Economy(buyer.Client, purchase); await Economy(buyer.Client, purchase);
        await using var db = await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var city = await db.Villages.SingleAsync(x => x.Slug == world.Slug);
        var accounts = await db.Citizens.Where(x => x.VillageId == city.Id).ToDictionaryAsync(x => x.Id, x => x.AccountId);
        var wallets = await db.GameWallets.Where(x => x.VillageId == city.Id).ToDictionaryAsync(x => x.AccountId);
        Assert.Equal(118, wallets[accounts[seller.Id]].Coins); Assert.Equal(100 - cost - 20, wallets[accounts[buyer.Id]].Coins);
        Assert.Equal(2, wallets[accounts[seller.Id]].TaxPaidToday); Assert.Equal(20, wallets[accounts[seller.Id]].TaxableProfitToday);
        Assert.Equal(100 + cost + 2, (await db.KingdomEconomies.SingleAsync(x => x.VillageId == city.Id)).TreasuryCoins);
        var claims = await db.WorkOrderClaims.Where(x => x.VillageId == city.Id).ToListAsync();
        Assert.Equal(2, claims.Count); Assert.All(claims, x => Assert.Equal(WorkClaimStatus.Paid, x.Status));
        Assert.Equal(0, (await db.WorkOrders.SingleAsync(x => x.Id == orderId)).ReservedCoins);
        var batches = await db.MonetaryBatches.Where(x => x.VillageId == city.Id).ToListAsync();
        Assert.Equal(300, batches.Sum(x => x.Issued)); Assert.Equal(0, batches.Sum(x => x.Withdrawn));
        Assert.Equal(300, await db.LedgerEntries.Where(x => x.VillageId == city.Id && x.Asset == "DZK").SumAsync(x => x.Delta));
        Assert.DoesNotContain(batches.SelectMany(x => x.Operations), x => x.Kind == "legacy-wage-issuance");
        Assert.Equal(1, await db.MarketTaxEvents.CountAsync(x => x.VillageId == city.Id));

        // A separate private cycle creates personal goods, never a second public wage or common output.
        var commonBeforePrivate = (await db.Villages.AsNoTracking().SingleAsync(x => x.Id == city.Id)).Stock(ResourceKind.Wood);
        await Players.OkJsonAsync(await seller.Client.PutAsJsonAsync(world.Path + "/me/rest", new { resting = false }));
        await Players.OkJsonAsync(await seller.Client.PostAsJsonAsync(world.Path + "/me/task", new { taskId = "chop-wood", ownership = "PrivateExtraction" }));
        await Players.OkJsonAsync(await seller.Client.PutAsJsonAsync(world.Path + "/me/rest", new { resting = true }));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));
        await Players.OkJsonAsync(await seller.Client.GetAsync(world.Path + "/me"));
        var privateProducer = await db.Citizens.AsNoTracking().SingleAsync(x => x.Id == seller.Id);
        var ownWood = Assert.Single(privateProducer.PersonalGoods, x => x.Kind == ResourceKind.Wood);
        Assert.True(ownWood.Amount > 0); Assert.True(ownWood.CostBasisKnown); Assert.Equal(0, ownWood.CostBasis);
        Assert.Equal(commonBeforePrivate, (await db.Villages.AsNoTracking().SingleAsync(x => x.Id == city.Id)).Stock(ResourceKind.Wood));
        Assert.Equal(118, (await db.GameWallets.AsNoTracking().SingleAsync(x => x.VillageId == city.Id && x.AccountId == accounts[seller.Id])).Coins);
        var privateLot = Guid.NewGuid(); await Economy(seller.Client, new(privateLot, "list", ResourceKind.Wood, 1, 20));
        var privateQuote = await Players.OkJsonAsync(await buyer.Client.PostAsJsonAsync(world.Path + "/economy/market-quote", new MarketTaxQuoteRequest(Guid.NewGuid(), privateLot)));
        var privateSale = new EconomyRequest(Guid.NewGuid(), "buy-lot", Target: privateLot, TaxQuoteId: privateQuote.GetProperty("id").GetGuid());
        await Economy(buyer.Client, privateSale); await Economy(buyer.Client, privateSale);
        Assert.Equal(136, (await db.GameWallets.AsNoTracking().SingleAsync(x => x.VillageId == city.Id && x.AccountId == accounts[seller.Id])).Coins);
        Assert.Equal(100 - cost - 40, (await db.GameWallets.AsNoTracking().SingleAsync(x => x.VillageId == city.Id && x.AccountId == accounts[buyer.Id])).Coins);
        Assert.Equal(100 + cost + 4, (await db.KingdomEconomies.AsNoTracking().SingleAsync(x => x.VillageId == city.Id)).TreasuryCoins);
        Assert.Equal(300, await db.MonetaryBatches.Where(x => x.VillageId == city.Id).SumAsync(x => x.Issued));
        Assert.Equal(2, await db.MarketTaxEvents.CountAsync(x => x.VillageId == city.Id));
        await Players.AssertRuleBrokenAsync(await world.Tsar.PostAsJsonAsync(world.Path + "/economy/treasury-issue",
            new TreasuryIssueRequest(Guid.NewGuid(), 1, state.BudgetDay, 1, 1)), "funding.allowance");
        factory.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(issueReceipt.GetRawText(), (await Players.OkJsonAsync(await world.Tsar.PostAsJsonAsync(world.Path + "/economy/treasury-issue", issue))).GetRawText());
        Assert.Equal(300, await db.TreasuryIssues.Where(x => x.VillageId == city.Id).SumAsync(x => x.Amount));
    }
}
