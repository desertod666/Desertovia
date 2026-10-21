using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DirectorEconomyApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private GameService Game=>factory.Services.GetRequiredService<GameService>();
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static LedgerEntry Wage(Guid world,Guid account,DateTimeOffset at,long amount,string operation="wage")=>
        new(){VillageId=world,AccountId=account,OperationId=Guid.NewGuid(),Operation=operation,Asset="DZK",Location="wallet",Delta=amount,At=at};
    private static JsonElement HistoryValue(JsonElement history,JsonElement row,string column)
    {
        var columns=history.GetProperty("columns").EnumerateArray().Select(x=>x.GetString()).ToArray();
        return row[Array.IndexOf(columns,column)];
    }
    private static MarketLot Sold(Guid world,Guid seller,Guid buyer,DateTimeOffset at,long price,long cost,bool known)=>
        new(){VillageId=world,SellerAccountId=seller,BuyerAccountId=buyer,Resource=ResourceKind.Wood,Amount=1,Price=price,
            CostBasis=cost,CostBasisKnown=known,Status="sold",CreatedAt=at.AddHours(-1),ClosedAt=at};

    [Fact]
    public async Task Director_sees_every_resident_but_never_treats_wages_unknown_cost_or_transfers_as_private_profit()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Economic evidence");
        var a=await players.JoinAsync(v,"Producer");var b=await players.JoinAsync(v,"Legacy seller");var c=await players.JoinAsync(v,"Resting observer");
        await using var db=await Db();var world=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        var residents=await db.Citizens.Where(x=>x.VillageId==world.Id).ToListAsync();
        var actor=residents.Single(x=>x.Id==a.Id).AccountId;var other=residents.Single(x=>x.Id==b.Id).AccountId;
        var today=new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date,TimeSpan.Zero);var yesterday=today.AddHours(-6);
        foreach(var resident in residents)resident.CreatedAt=today.AddDays(-4);
        var economy=await db.KingdomEconomies.SingleAsync(x=>x.VillageId==world.Id);economy.StartedAt=today.AddDays(-3);
        db.LedgerEntries.AddRange(Wage(world.Id,actor,today.AddDays(-3).AddHours(12),10),
            Wage(world.Id,actor,yesterday,100),Wage(world.Id,actor,yesterday,20,"contract-wage"),Wage(world.Id,other,yesterday,20),
            Wage(world.Id,actor,today,10),Wage(world.Id,actor,today.AddDays(1),900000));
        db.LedgerEntries.AddRange(
            new LedgerEntry{VillageId=world.Id,OperationId=Guid.NewGuid(),AccountId=actor,Operation="trade",Asset="DZK",Location="wallet",Delta=300,At=yesterday},
            new LedgerEntry{VillageId=world.Id,OperationId=Guid.NewGuid(),AccountId=other,Operation="trade",Asset="DZK",Location="wallet",Delta=-300,At=yesterday});
        var sold=Sold(world.Id,actor,other,yesterday,300,100,true);
        db.MarketLots.AddRange(sold,Sold(world.Id,other,actor,yesterday,5000,0,false),Sold(world.Id,actor,other,yesterday,10,30,true));
        var supply=new SupplyOrder{VillageId=world.Id,IssuerAccountId=world.OwnerAccountId,Resource=ResourceKind.Wood,
            CreatedAt=today.AddDays(-2),ExpiresAt=today.AddDays(1),ReservedCoins=400,Status=SupplyOrderStatus.Open};db.SupplyOrders.Add(supply);
        db.SupplyDeliveries.Add(new(){Id=Guid.NewGuid(),VillageId=world.Id,SupplyOrderId=supply.Id,SupplierAccountId=actor,
            Resource=ResourceKind.Wood,Amount=1,PaidCoins=80,CostBasis=60,CostBasisKnown=true,At=yesterday});
        var quote=new MarketTaxQuote{VillageId=world.Id,Id=Guid.NewGuid(),LotId=sold.Id,BuyerAccountId=other,SellerAccountId=actor,
            Price=300,CostBasis=100,CostBasisKnown=true,Day=DateOnly.FromDateTime(yesterday.UtcDateTime),CreatedAt=yesterday,ExpiresAt=yesterday.AddMinutes(2)};
        db.MarketTaxQuotes.Add(quote);db.MarketTaxEvents.Add(new(){VillageId=world.Id,Id=Guid.NewGuid(),QuoteId=quote.Id,LotId=sold.Id,
            SellerAccountId=actor,Price=300,CostBasis=100,CostBasisKnown=true,RealizedProfit=200,TaxableGain=200,TaxAmount=10,At=yesterday});
        db.GameWallets.Add(new(){VillageId=world.Id,AccountId=actor,Coins=123,IncomeDay=DateOnly.FromDateTime(today.UtcDateTime).AddDays(-7),EarnedToday=999999,ProfitToday=888888});
        await db.SaveChangesAsync();
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        using var json=JsonDocument.Parse(review.Snapshot);var analysis=json.RootElement.GetProperty("economy").GetProperty("analysis");
        Assert.Equal(3,analysis.GetProperty("currentResidents").GetInt32());
        var complete=analysis.GetProperty("completeDays");Assert.Equal(2,complete.GetProperty("rows").GetArrayLength());
        var day=complete.GetProperty("rows").EnumerateArray().Single(x=>HistoryValue(complete,x,"day").GetString()==DateOnly.FromDateTime(yesterday.UtcDateTime).ToString("yyyy-MM-dd"));
        Assert.Equal(140,HistoryValue(complete,day,"wageTotal").GetDecimal());
        Assert.Equal(2,HistoryValue(complete,day,"wageRecipients").GetInt32());
        Assert.Equal(3,HistoryValue(complete,day,"residentsEligibleByDayEnd").GetInt32());
        Assert.Equal(70,HistoryValue(complete,day,"wageMedianAmongRecipients").GetDecimal());
        Assert.Equal(220,HistoryValue(complete,day,"knownPrivatePositiveProfit").GetDecimal());
        Assert.Equal(20,HistoryValue(complete,day,"knownPrivateLoss").GetDecimal());
        Assert.Equal(5310,HistoryValue(complete,day,"marketRevenue").GetDecimal());
        Assert.Equal(80,HistoryValue(complete,day,"supplyRevenue").GetDecimal());
        Assert.Equal(5000,HistoryValue(complete,day,"unknownCostRevenue").GetDecimal());
        Assert.Equal(1,HistoryValue(complete,day,"unknownCostSales").GetInt32());
        Assert.Equal(10,HistoryValue(complete,day,"taxPaid").GetDecimal());
        Assert.Equal(1,HistoryValue(complete,day,"taxedPeople").GetInt32());
        Assert.Equal(4.5455m,HistoryValue(complete,day,"highestIndividualTaxPercent").GetDecimal());
        Assert.Equal(10,analysis.GetProperty("currentPartialDay").GetProperty("wages").GetProperty("total").GetDecimal());
        Assert.False(analysis.GetProperty("currentPartialDay").GetProperty("complete").GetBoolean());
        Assert.Equal(10,json.RootElement.GetProperty("economy").GetProperty("wagesToday").GetDecimal());
        var flows=analysis.GetProperty("moneyFlows").GetProperty("categories");
        Assert.Equal(0,flows.EnumerateArray().Single(x=>x.GetProperty("operation").GetString()=="trade").GetProperty("recordedNetChange").GetDecimal());
        Assert.Equal(400,analysis.GetProperty("funds").GetProperty("supplyReserved").GetDecimal());
        Assert.Equal(123,(await db.GameWallets.AsNoTracking().SingleAsync(x=>x.VillageId==world.Id&&x.AccountId==actor)).Coins);
        Assert.DoesNotContain(actor.ToString(),analysis.GetRawText());Assert.DoesNotContain("Producer",analysis.GetRawText());
        Assert.DoesNotContain(review.Options,x=>x.Id.Contains("tax"));
        Assert.True(analysis.GetProperty("taxPolicy").GetProperty("automaticEconomicExecutorAvailable").GetBoolean());
        Assert.True(analysis.GetProperty("taxPolicy").GetProperty("requiresExplicitDelegationAndAutoEconomy").GetBoolean());
    }

    [Fact]
    public async Task New_economy_does_not_fabricate_fourteen_complete_days_or_income_from_wallet_balances()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("No economic history");
        var resident=await players.JoinAsync(v,"New farmer");await using var db=await Db();var world=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        var actor=await db.Citizens.Where(x=>x.Id==resident.Id).Select(x=>x.AccountId).SingleAsync();
        db.GameWallets.Add(new(){VillageId=world.Id,AccountId=actor,Coins=999999,IncomeDay=DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime),EarnedToday=100000});
        await db.SaveChangesAsync();var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        using var json=JsonDocument.Parse(review.Snapshot);var analysis=json.RootElement.GetProperty("economy").GetProperty("analysis");
        Assert.Empty(analysis.GetProperty("completeDays").GetProperty("rows").EnumerateArray());
        Assert.Equal(0,analysis.GetProperty("currentPartialDay").GetProperty("wages").GetProperty("total").GetDecimal());
        Assert.Equal(999999,analysis.GetProperty("funds").GetProperty("walletCoins").GetDecimal());
        Assert.True(analysis.GetProperty("taxPolicy").GetProperty("unknownCostExempt").GetBoolean());
    }

    [Fact]
    public async Task Observation_limits_history_to_fourteen_UTC_days_and_never_reads_another_world_or_future_receipts()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Economic history window");
        var other=await players.FoundVillageAsync("Other economic world");var resident=await players.JoinAsync(v,"Window worker");
        await using var db=await Db();var world=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var otherId=await db.Villages.Where(x=>x.Slug==other.Slug).Select(x=>x.Id).SingleAsync();
        var citizen=await db.Citizens.SingleAsync(x=>x.Id==resident.Id);var today=new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date,TimeSpan.Zero);
        citizen.CreatedAt=today.AddDays(-30);(await db.KingdomEconomies.SingleAsync(x=>x.VillageId==world.Id)).StartedAt=today.AddDays(-30);
        db.LedgerEntries.AddRange(Wage(world.Id,citizen.AccountId,today.AddDays(-30),90000),Wage(world.Id,citizen.AccountId,today.AddDays(-15),80000),
            Wage(world.Id,citizen.AccountId,today.AddDays(-14),20),Wage(world.Id,citizen.AccountId,today,10),
            Wage(world.Id,citizen.AccountId,today.AddDays(1),70000),Wage(otherId,citizen.AccountId,today,60000));
        await db.SaveChangesAsync();var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        using var json=JsonDocument.Parse(review.Snapshot);var analysis=json.RootElement.GetProperty("economy").GetProperty("analysis");
        var days=analysis.GetProperty("completeDays");Assert.Equal(14,days.GetProperty("rows").GetArrayLength());
        Assert.Equal(20,days.GetProperty("rows").EnumerateArray().Sum(x=>HistoryValue(days,x,"wageTotal").GetDecimal()));
        Assert.Equal(10,analysis.GetProperty("currentPartialDay").GetProperty("wages").GetProperty("total").GetDecimal());
        Assert.Equal(30,analysis.GetProperty("moneyFlows").GetProperty("recordedNetChange").GetDecimal());
        Assert.True(analysis.GetRawText().Length<40000,"Aggregate model payload must not include the raw journal or per-account rows.");
    }
}
