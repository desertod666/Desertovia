using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

internal sealed record DirectorDistribution(int People,int Recipients,int WithoutRecordedIncome,decimal Total,
    decimal MeanAmongRecipients,decimal MedianAmongRecipients,decimal P90AmongRecipients,decimal Maximum);
internal sealed record DirectorEconomicDay(DateOnly Day,bool Complete,int ResidentsEligibleByDayEnd,int ObservedFinancialActors,
    DirectorDistribution Wages,DirectorDistribution KnownPrivatePositiveProfit,decimal KnownPrivateLoss,
    decimal MarketRevenue,decimal SupplyRevenue,int KnownSales,int UnknownCostSales,decimal UnknownCostRevenue,
    decimal TaxPaid,decimal? TaxPercentOfKnownPositiveProfit,int TaxedPeople,decimal? HighestIndividualTaxPercent,
    decimal TaxWithoutObservedKnownPositiveProfit);
internal sealed record DirectorEconomicObservation(int Version,DateTimeOffset CapturedAt,DateTimeOffset WindowStart,
    DateTimeOffset? AccountingStartedAt,DateTimeOffset? FirstLedgerEntry,int CurrentResidents,
    object CompleteDays,DirectorEconomicDay CurrentPartialDay,
    object Funds,object MoneyFlows,object TaxPolicy,object Food,IReadOnlyList<string> Interpretation);

public sealed partial class GameService
{
    // The model receives bounded aggregates, never names, account IDs or the raw journal.
    // Each actor participates in the server calculation; no top-N sampling of rich players.
    private sealed record EconomicRow(DateOnly Day,Guid Account,decimal Wages=0,decimal Revenue=0,
        decimal Gain=0,decimal Loss=0,int Known=0,int Unknown=0,decimal UnknownRevenue=0,decimal Tax=0);

    private static DirectorDistribution EconomicDistribution(IEnumerable<decimal> amounts)
    {
        var values=amounts.ToArray();var positive=values.Where(x=>x>0).Order().ToArray();
        decimal Percentile(decimal percentile)
        {
            if(positive.Length==0)return 0;
            var index=(positive.Length-1)*percentile;var lo=(int)decimal.Floor(index);var hi=(int)decimal.Ceiling(index);
            return positive[lo]+(positive[hi]-positive[lo])*(index-lo);
        }
        return new(values.Length,positive.Length,values.Length-positive.Length,values.Sum(),
            positive.Length==0?0:positive.Average(),Percentile(.5m),Percentile(.9m),positive.LastOrDefault());
    }

    private static async Task<DirectorEconomicObservation> ObserveDirectorEconomyAsync(DesertodDbContext db,Village village,
        KingdomEconomy? economy,TaxPolicy? policy,DateTimeOffset now,CancellationToken ct)
    {
        var today=new DateTimeOffset(now.UtcDateTime.Date,TimeSpan.Zero);var start=today.AddDays(-14);
        var ledger=db.LedgerEntries.AsNoTracking().Where(x=>x.VillageId==village.Id&&x.At>=start&&x.At<=now);
        var first=await db.LedgerEntries.Where(x=>x.VillageId==village.Id).MinAsync(x=>(DateTimeOffset?)x.At,ct);
        var residents=await db.Citizens.AsNoTracking().Where(x=>x.VillageId==village.Id)
            .Select(x=>new{x.AccountId,x.CreatedAt,x.IsHungry}).ToListAsync(ct);
        var wages=await ledger.Where(x=>x.Asset=="DZK"&&x.Location=="wallet"&&x.AccountId!=null
                &&(x.Operation=="wage"||x.Operation=="contract-wage")&&x.Delta>0)
            .GroupBy(x=>new{Day=DateOnly.FromDateTime(x.At.UtcDateTime),Account=x.AccountId!.Value})
            .Select(g=>new EconomicRow(g.Key.Day,g.Key.Account,g.Sum(x=>(decimal)x.Delta),0,0,0,0,0,0,0)).ToListAsync(ct);
        var market=await db.MarketLots.AsNoTracking().Where(x=>x.VillageId==village.Id&&x.Status=="sold"&&x.ClosedAt>=start&&x.ClosedAt<=now)
            .GroupBy(x=>new{Day=DateOnly.FromDateTime(x.ClosedAt!.Value.UtcDateTime),Account=x.SellerAccountId})
            .Select(g=>new EconomicRow(g.Key.Day,g.Key.Account,0,g.Sum(x=>(decimal)x.Price),
                g.Sum(x=>x.CostBasisKnown&&x.Price>x.CostBasis?(decimal)x.Price-x.CostBasis:0),
                g.Sum(x=>x.CostBasisKnown&&x.CostBasis>x.Price?(decimal)x.CostBasis-x.Price:0),
                g.Count(x=>x.CostBasisKnown),g.Count(x=>!x.CostBasisKnown),g.Sum(x=>!x.CostBasisKnown?(decimal)x.Price:0),0)).ToListAsync(ct);
        var supplies=await db.SupplyDeliveries.AsNoTracking().Where(x=>x.VillageId==village.Id&&x.At>=start&&x.At<=now)
            .GroupBy(x=>new{Day=DateOnly.FromDateTime(x.At.UtcDateTime),Account=x.SupplierAccountId})
            .Select(g=>new EconomicRow(g.Key.Day,g.Key.Account,0,g.Sum(x=>(decimal)x.PaidCoins),
                g.Sum(x=>x.CostBasisKnown&&x.PaidCoins>x.CostBasis?(decimal)x.PaidCoins-x.CostBasis:0),
                g.Sum(x=>x.CostBasisKnown&&x.CostBasis>x.PaidCoins?(decimal)x.CostBasis-x.PaidCoins:0),
                g.Count(x=>x.CostBasisKnown),g.Count(x=>!x.CostBasisKnown),g.Sum(x=>!x.CostBasisKnown?(decimal)x.PaidCoins:0),0)).ToListAsync(ct);
        var taxes=await db.MarketTaxEvents.AsNoTracking().Where(x=>x.VillageId==village.Id&&x.At>=start&&x.At<=now)
            .GroupBy(x=>new{Day=DateOnly.FromDateTime(x.At.UtcDateTime),Account=x.SellerAccountId})
            .Select(g=>new EconomicRow(g.Key.Day,g.Key.Account,0,0,0,0,0,0,0,g.Sum(x=>(decimal)x.TaxAmount))).ToListAsync(ct);
        var rows=wages.Concat(market).Concat(supplies).Concat(taxes).ToLookup(x=>x.Day);
        DirectorEconomicDay Summarize(DateTime day,bool complete)
        {
            var eligible=residents.Where(x=>x.CreatedAt.UtcDateTime.Date<=day).Select(x=>x.AccountId).ToHashSet();
            var dayKey=DateOnly.FromDateTime(day);var dayRows=rows[dayKey].ToArray();var actors=dayRows.Select(x=>x.Account).Distinct().ToArray();
            var all=eligible.Union(actors).ToArray();
            var wageByActor=dayRows.GroupBy(x=>x.Account).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Wages));
            var gainByActor=dayRows.GroupBy(x=>x.Account).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Gain));
            var taxByActor=dayRows.GroupBy(x=>x.Account).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Tax));
            var profit=gainByActor.Values.Sum();var tax=dayRows.Sum(x=>x.Tax);
            return new(DateOnly.FromDateTime(day),complete,eligible.Count,actors.Length,
                EconomicDistribution(all.Select(a=>wageByActor.GetValueOrDefault(a))),
                EconomicDistribution(all.Select(a=>gainByActor.GetValueOrDefault(a))),dayRows.Sum(x=>x.Loss),
                market.Where(x=>x.Day==dayKey).Sum(x=>x.Revenue),supplies.Where(x=>x.Day==dayKey).Sum(x=>x.Revenue),
                dayRows.Sum(x=>x.Known),dayRows.Sum(x=>x.Unknown),dayRows.Sum(x=>x.UnknownRevenue),tax,
                profit>0?decimal.Round(tax/profit*100,4):null,taxByActor.Count(x=>x.Value>0),
                taxByActor.Where(x=>gainByActor.GetValueOrDefault(x.Key)>0)
                    .Select(x=>(decimal?)decimal.Round(x.Value/gainByActor[x.Key]*100,4)).Max(),
                taxByActor.Where(x=>gainByActor.GetValueOrDefault(x.Key)==0).Sum(x=>x.Value));
        }
        // A partial first accounting day is not passed off as a full zero-income day.
        var reliableStart=economy?.StartedAt;
        if(first is {} firstAt&&(reliableStart is null||firstAt>reliableStart))reliableStart=firstAt;
        var days=new List<DirectorEconomicDay>();
        for(var day=start;day<today;day=day.AddDays(1))
            if(reliableStart is {} since&&day>=since)days.Add(Summarize(day.UtcDateTime,true));
        var flows=await ledger.Where(x=>x.Asset=="DZK"&&x.Operation!="opening")
            .GroupBy(x=>x.Operation).Select(g=>new{operation=g.Key,entries=g.Count(),recordedNetChange=g.Sum(x=>(decimal)x.Delta),
                walletCredits=g.Sum(x=>x.Location=="wallet"&&x.Delta>0?(decimal)x.Delta:0),
                walletDebits=g.Sum(x=>x.Location=="wallet"&&x.Delta<0?-(decimal)x.Delta:0)})
            .OrderBy(x=>x.operation).ToListAsync(ct);
        var workReserved=await db.WorkOrders.Where(x=>x.VillageId==village.Id).SumAsync(x=>(decimal)x.ReservedCoins,ct);
        var supplyReserved=await db.SupplyOrders.Where(x=>x.VillageId==village.Id).SumAsync(x=>(decimal)x.ReservedCoins,ct);
        var promised=await db.WorkOrderClaims.Where(x=>x.VillageId==village.Id&&(x.Status==WorkClaimStatus.Working||x.Status==WorkClaimStatus.Payable))
            .SumAsync(x=>(decimal)x.PromisedPay,ct);
        var balances=await db.GameWallets.Where(x=>x.VillageId==village.Id).Select(x=>(decimal)x.Coins).ToListAsync(ct);
        var wallets=balances.Sum();var wealth=EconomicDistribution(balances);
        var auditQuery=db.MonetaryBatches.AsNoTracking().Where(x=>x.VillageId==village.Id&&x.Finalized&&x.At>=start&&x.At<=now);
        var auditTotals=await auditQuery.GroupBy(x=>1).Select(g=>new{batches=g.Count(),postings=g.Sum(x=>x.PostingCount),
            issued=g.Sum(x=>x.Issued),withdrawn=g.Sum(x=>x.Withdrawn),transferred=g.Sum(x=>x.Transferred)}).SingleOrDefaultAsync(ct);
        var lastAudit=await db.MonetaryBatches.AsNoTracking().Where(x=>x.VillageId==village.Id&&x.Finalized&&x.At<=now)
            .OrderByDescending(x=>x.At).ThenByDescending(x=>x.Id).Select(x=>new{x.At,x.AfterHash}).FirstOrDefaultAsync(ct);
        var unvalidated=await ledger.CountAsync(x=>x.Asset=="DZK"&&x.FinancialBatchId==null,ct);
        var liveHash=HoldingHash(await SnapshotMoneyAsync(db,village.Id,ct));
        var anchors=await ObserveMonetaryAnchorsAsync(db,village.Id,start,now,ct);
        var funding=await FundingStateAsync(db,village.Id,now,ct);
        var history=new{
            columns=new[]{"day","residentsEligibleByDayEnd","observedFinancialActors","financialCohortPeople","withoutRecordedWages","withoutKnownPrivateProfit","wageRecipients","wageTotal","wageMedianAmongRecipients","wageP90AmongRecipients","wageMaximum",
                "profitRecipients","knownPrivatePositiveProfit","profitMedianAmongRecipients","profitP90AmongRecipients","profitMaximum","knownPrivateLoss",
                "marketRevenue","supplyRevenue","knownSales","unknownCostSales","unknownCostRevenue","taxPaid","taxPercentOfKnownPositiveProfit","taxedPeople","highestIndividualTaxPercent","taxWithoutObservedKnownPositiveProfit"},
            rows=days.Select(d=>new object?[]{d.Day,d.ResidentsEligibleByDayEnd,d.ObservedFinancialActors,d.Wages.People,d.Wages.WithoutRecordedIncome,d.KnownPrivatePositiveProfit.WithoutRecordedIncome,d.Wages.Recipients,d.Wages.Total,d.Wages.MedianAmongRecipients,d.Wages.P90AmongRecipients,d.Wages.Maximum,
                d.KnownPrivatePositiveProfit.Recipients,d.KnownPrivatePositiveProfit.Total,d.KnownPrivatePositiveProfit.MedianAmongRecipients,d.KnownPrivatePositiveProfit.P90AmongRecipients,d.KnownPrivatePositiveProfit.Maximum,d.KnownPrivateLoss,
                d.MarketRevenue,d.SupplyRevenue,d.KnownSales,d.UnknownCostSales,d.UnknownCostRevenue,d.TaxPaid,d.TaxPercentOfKnownPositiveProfit,d.TaxedPeople,d.HighestIndividualTaxPercent,d.TaxWithoutObservedKnownPositiveProfit}).ToArray()
        };
        return new(1,now,start,economy?.StartedAt,first,residents.Count,history,Summarize(today.UtcDateTime,false),
            new{treasury=economy?.TreasuryCoins??0,walletCoins=wallets,workReserved,supplyReserved,
                treasuryFunding=new{funding.LatestVersion,
                    active=funding.Current is {} fund?new{fund.Version,fund.Enabled,fund.DailyIssueLimit,fund.EffectiveAt}:null,
                    scheduled=new{columns=new[]{"version","enabled","dailyIssueLimit","effectiveAt"},
                        rows=funding.Scheduled.Select(p=>new object[]{p.Version,p.Enabled,p.DailyIssueLimit,p.EffectiveAt}),
                        count=funding.ScheduledCount,truncated=funding.ScheduledTruncated},
                    funding.BudgetDay,funding.BudgetRevision,funding.IssuedToday,funding.RemainingAllowance,funding.FundedWorkEnabled,
                    automaticIssueExecutorAvailable=false,allowanceIsNotMoney=true},
                acceptedWorkPromises=promised,promisesAreSubsetOfWorkReserved=true,
                totalRecordedMoney=wallets+(economy?.TreasuryCoins??0)+workReserved+supplyReserved,
                walletBalances=new{holders=wealth.People,zeroBalances=wealth.WithoutRecordedIncome,
                    medianAmongNonzero=wealth.MedianAmongRecipients,p90AmongNonzero=wealth.P90AmongRecipients,maximum=wealth.Maximum}},
            new{recordedNetChange=flows.Sum(x=>x.recordedNetChange),categories=flows.Take(40),categoryCount=flows.Count,
                categoriesTruncated=flows.Count>40,meaning="Net of all recorded locations; transfers are not issuance, wallet credits are not automatically profit.",
                validated=new{windowStart=start,through=now,totals=auditTotals,legacyPostingsWithoutValidation=unvalidated,
                    anchorChanges=anchors.Changes,unexplainedAnchorNet=anchors.NetChange,anchorsAreNotIssuance=true,
                    lastAuditAt=lastAudit?.At,currentActionNotYetFinalized=db.FinancialBatchStarted,
                    currentHoldingsMatchLastAudit=lastAudit is null?(bool?)null:lastAudit.AfterHash==liveHash,
                    meaning="Finalized server receipts reconcile every wallet, treasury and reserve. Unvalidated history and externally changed anchors are not proven issuance. A pending current action is finalized after this observation."}},
            new{newOfferPolicyVersion=policy?.Version,newOffersTaxEnabled=policy?.Enabled??false,
                brackets=policy?.Brackets??[],settlement="Frozen offer terms: older enabled policies may still settle when new offers are tax-free.",
                wagesExempt=true,unknownCostExempt=true,automaticEconomicExecutorAvailable=false},
            new{communalFood=village.Storages.Where(x=>x.OwnerAccountId is null).SelectMany(x=>x.Goods).Where(x=>x.Kind==ResourceKind.Food).Sum(x=>x.Amount),
                hungryResidents=residents.Count(x=>x.IsHungry),grainIsNotReadyFood=true,
                farmEntitlementsNotYetIncluded=true},
            new[]{"All residents and recorded financial actors are included; recipient percentiles exclude zeros, withoutRecordedIncome includes eligible residents without receipts. No activity or hours are inferred from income.",
                "completeDays is a column-labelled table of full UTC days; currentPartialDay is unfinished. Days before accounting/history coverage are omitted. Missing receipts cannot prove zero economic activity.",
                "Known private profit includes market and public supply deliveries; supply tax is not implemented. Wages are separate and exempt. Positive gains and losses are separate; unknown cost is not zero cost.",
                "Balances and opening stock are not earnings. TotalRecordedMoney covers wallets/treasury/work/supply funds. Treasury funding allowance is a ceiling, not issued money or a promise to spend it. New public wages need backed orders; old accepted wages retain their terms. Historical completeness is not guaranteed.",
                "Analyze affordability, inequality of burden, treasury obligations and food; use actual evidence and acknowledge short history. Exact tax settings are for the director to assess, not fixed defaults. Never create retrospective debt or modify accepted offer terms.",
                "This observation is analysis, not authority to debit wallets. Future autonomous fiscal actions require versioned executors, fresh validation, limits, audit and owner category controls. Dcoin and real payments are outside this analysis."});
    }
}
