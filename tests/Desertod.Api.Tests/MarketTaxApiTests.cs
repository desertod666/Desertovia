using System.Net;
using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class MarketTaxApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task<(FoundedVillage World,Resident Seller,Resident Buyer)> Setup(string name)
    {
        var world=await players.FoundVillageAsync(name);var seller=await players.JoinAsync(world,"Seller");var buyer=await players.JoinAsync(world,"Buyer");
        await buyer.Client.GetAsync(world.Path+"/economy");await seller.Client.GetAsync(world.Path+"/economy");
        await using var db=await Db();var c=await db.Citizens.SingleAsync(x=>x.Id==buyer.Id);
        (await db.GameWallets.SingleAsync(x=>x.VillageId==c.VillageId&&x.AccountId==c.AccountId)).Coins=10000;
        await db.SaveChangesAsync();return(world,seller,buyer);
    }
    private async Task<TaxPolicyRequest> Policy(FoundedVillage world,long expected,bool enabled=true)
    {
        var request=new TaxPolicyRequest(Guid.NewGuid(),expected,enabled,factory.Clock.GetUtcNow(),
            enabled?[new(){FromProfit=0,RateBasisPoints=500}]:[]);
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/tax-policy",request));return request;
    }
    private async Task<Guid> Lot(FoundedVillage world,Resident seller,long price,long cost=100,bool known=true)
    {
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(x=>x.Id==seller.Id);PersonalInventory.Add(c,ResourceKind.Wood,1,cost,known);await db.SaveChangesAsync();
        }
        var id=Guid.NewGuid();await Players.OkJsonAsync(await seller.Client.PostAsJsonAsync(world.Path+"/economy",new EconomyRequest(id,"list",ResourceKind.Wood,1,price)));return id;
    }
    private static async Task<Guid> Quote(FoundedVillage world,Resident buyer,Guid lot)
    {
        var id=Guid.NewGuid();var json=await Players.OkJsonAsync(await buyer.Client.PostAsJsonAsync(world.Path+"/economy/market-quote",new MarketTaxQuoteRequest(id,lot)));
        Assert.Equal(id,json.GetProperty("id").GetGuid());Assert.False(json.TryGetProperty("costBasis",out _));Assert.False(json.TryGetProperty("taxAmount",out _));
        return id;
    }

    [Fact]
    public async Task Accepted_offer_keeps_old_policy_and_purchase_transfers_one_tax_once_without_minting_or_changing_buyer_cost()
    {
        using var frozen=factory.Clock.Freeze();var (world,seller,buyer)=await Setup("Frozen fiscal terms");
        var legacy=await Lot(world,seller,300);await Policy(world,0);var taxed=await Lot(world,seller,300);
        var quote=await Quote(world,buyer,taxed);await Policy(world,1,false);
        var request=new EconomyRequest(Guid.NewGuid(),"buy-lot",Target:taxed,TaxQuoteId:quote);
        var responses=await Task.WhenAll(buyer.Client.PostAsJsonAsync(world.Path+"/economy",request),buyer.Client.PostAsJsonAsync(world.Path+"/economy",request));
        foreach(var response in responses)await Players.OkJsonAsync(response);
        await Players.OkJsonAsync(await buyer.Client.PostAsJsonAsync(world.Path+"/economy",new EconomyRequest(Guid.NewGuid(),"buy-lot",Target:legacy)));
        await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==world.Slug);
        var accounts=await db.Citizens.Where(x=>x.VillageId==city.Id).ToDictionaryAsync(x=>x.Id,x=>x.AccountId);
        var sellerWallet=await db.GameWallets.SingleAsync(x=>x.VillageId==city.Id&&x.AccountId==accounts[seller.Id]);
        var buyerWallet=await db.GameWallets.SingleAsync(x=>x.VillageId==city.Id&&x.AccountId==accounts[buyer.Id]);
        Assert.Equal(590,sellerWallet.Coins);Assert.Equal(9400,buyerWallet.Coins);
        Assert.Equal(10,sellerWallet.TaxPaidToday);Assert.Equal(200,sellerWallet.TaxableProfitToday);
        var economy=await db.KingdomEconomies.SingleAsync(x=>x.VillageId==city.Id);Assert.Equal(10,economy.TreasuryCoins);
        Assert.Equal(10000,sellerWallet.Coins+buyerWallet.Coins+economy.TreasuryCoins);
        Assert.Single(await db.MarketTaxEvents.Where(x=>x.VillageId==city.Id).ToListAsync());
        Assert.Equal(0,await db.LedgerEntries.Where(x=>x.VillageId==city.Id&&x.OperationId==request.RequestId&&x.Asset=="DZK").SumAsync(x=>x.Delta));
        Assert.Equal(600,(await db.Citizens.SingleAsync(x=>x.Id==buyer.Id)).PersonalGoods.Single(x=>x.Kind==ResourceKind.Wood).CostBasis);
        Assert.Null((await db.MarketLots.SingleAsync(x=>x.Id==legacy)).TaxPolicyVersion);
        Assert.Equal(1,(await db.MarketLots.SingleAsync(x=>x.Id==taxed)).TaxPolicyVersion);
    }

    [Fact]
    public async Task Owner_policy_receipts_are_replayable_but_not_reusable_or_stale_and_future_rules_do_not_apply_early()
    {
        using var frozen=factory.Clock.Freeze();var (world,seller,_)=await Setup("Published fiscal versions");
        var request=new TaxPolicyRequest(Guid.NewGuid(),0,true,factory.Clock.GetUtcNow().AddHours(1),[new(){FromProfit=0,RateBasisPoints=700}]);
        await Players.AssertForbiddenAsync(await seller.Client.PutAsJsonAsync(world.Path+"/economy/tax-policy",request));
        var first=await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/tax-policy",request));
        await Lot(world,seller,200);await Policy(world,1,false);
        var replay=await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/tax-policy",request));Assert.Equal(first.GetRawText(),replay.GetRawText());
        await Players.AssertRuleBrokenAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/tax-policy",request with{Enabled=false,Brackets=[]}),"tax.request.reused");
        await Players.AssertRuleBrokenAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/tax-policy",request with{RequestId=Guid.NewGuid()}),"tax.policy.stale");
        await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==world.Slug);
        Assert.Equal(2,await db.TaxPolicies.CountAsync(x=>x.VillageId==city.Id));Assert.Null((await db.MarketLots.SingleAsync(x=>x.VillageId==city.Id)).TaxPolicyVersion);
    }

    [Theory]
    [InlineData("profit_changed")]
    [InlineData("expired")]
    [InlineData("midnight")]
    public async Task Changed_settlement_state_requires_fresh_quote_without_spending(string reason)
    {
        using var frozen=factory.Clock.Freeze();var (world,seller,buyer)=await Setup("Stale fiscal quote "+reason);await Policy(world,0);
        var lot=await Lot(world,seller,300);var quote=await Quote(world,buyer,lot);
        if(reason=="profit_changed")
        {
            var other=await Lot(world,seller,300);var otherQuote=await Quote(world,buyer,other);
            await Players.OkJsonAsync(await buyer.Client.PostAsJsonAsync(world.Path+"/economy",new EconomyRequest(Guid.NewGuid(),"buy-lot",Target:other,TaxQuoteId:otherQuote)));
        }
        else if(reason=="expired")factory.Clock.Advance(TimeSpan.FromMinutes(2));
        else
        {
            // Keep expiry in the future: this specifically exercises the UTC-day check.
            await using var db=await Db();var stored=await db.MarketTaxQuotes.SingleAsync(x=>x.Id==quote);stored.ExpiresAt=factory.Clock.GetUtcNow().AddDays(2);await db.SaveChangesAsync();
            factory.Clock.Advance(TimeSpan.FromDays(1));
        }
        await Players.AssertRuleBrokenAsync(await buyer.Client.PostAsJsonAsync(world.Path+"/economy",new EconomyRequest(Guid.NewGuid(),"buy-lot",Target:lot,TaxQuoteId:quote)),"tax.quote.stale");
        await using var check=await Db();Assert.Equal("open",(await check.MarketLots.SingleAsync(x=>x.Id==lot)).Status);
        Assert.Null((await check.MarketTaxQuotes.SingleAsync(x=>x.Id==quote)).ConsumedAt);
    }

    [Theory]
    [InlineData(false,0,"unknown_cost")]
    [InlineData(true,400,"no_profit")]
    public async Task Unknown_cost_and_loss_never_create_tax_or_reduce_the_existing_daily_base(bool known,long cost,string exemption)
    {
        using var frozen=factory.Clock.Freeze();var (world,seller,buyer)=await Setup("Exempt sale "+exemption);await Policy(world,0);
        var lot=await Lot(world,seller,300,cost,known);var quote=await Quote(world,buyer,lot);
        await Players.OkJsonAsync(await buyer.Client.PostAsJsonAsync(world.Path+"/economy",new EconomyRequest(Guid.NewGuid(),"buy-lot",Target:lot,TaxQuoteId:quote)));
        await using var db=await Db();var settlement=await db.MarketTaxEvents.SingleAsync(x=>x.LotId==lot);
        Assert.Equal(0,settlement.TaxAmount);Assert.Equal(0,settlement.TaxableGain);Assert.Equal(exemption,settlement.ExemptReason);
        if(!known)Assert.Null(settlement.RealizedProfit);else Assert.Equal(-100,settlement.RealizedProfit);
    }

    [Fact]
    public async Task Unaffordable_purchase_rolls_back_quote_tax_treasury_and_receipt_together()
    {
        using var frozen=factory.Clock.Freeze();var (world,seller,buyer)=await Setup("Atomic fiscal settlement");await Policy(world,0);
        var lot=await Lot(world,seller,300);var quote=await Quote(world,buyer,lot);var id=Guid.NewGuid();
        await using(var db=await Db())
        {
            var account=await db.Citizens.Where(x=>x.Id==buyer.Id).Select(x=>x.AccountId).SingleAsync();
            (await db.GameWallets.SingleAsync(x=>x.AccountId==account)).Coins=1;await db.SaveChangesAsync();
        }
        await Players.AssertRuleBrokenAsync(await buyer.Client.PostAsJsonAsync(world.Path+"/economy",new EconomyRequest(id,"buy-lot",Target:lot,TaxQuoteId:quote)),"economy.funds");
        await using var check=await Db();Assert.Null((await check.MarketTaxQuotes.SingleAsync(x=>x.Id==quote)).ConsumedAt);
        Assert.Empty(await check.MarketTaxEvents.Where(x=>x.LotId==lot).ToListAsync());Assert.Empty(await check.EconomyReceipts.Where(x=>x.RequestId==id).ToListAsync());
        var city=await check.Villages.SingleAsync(x=>x.Slug==world.Slug);Assert.Equal(0,(await check.KingdomEconomies.SingleAsync(x=>x.VillageId==city.Id)).TreasuryCoins);
        Assert.Equal("open",(await check.MarketLots.SingleAsync(x=>x.Id==lot)).Status);
    }
}
