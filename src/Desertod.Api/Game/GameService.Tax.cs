using System.Security.Cryptography;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record TaxPolicyRequest(Guid RequestId,long? ExpectedVersion,bool? Enabled,DateTimeOffset? EffectiveAt,List<TaxBracket>? Brackets);
public sealed record TaxPolicyDto(long Version,Guid RequestId,bool Enabled,DateTimeOffset EffectiveAt,DateTimeOffset PublishedAt,IReadOnlyList<TaxBracket> Brackets);
public sealed record TaxPolicyStateDto(long LatestVersion,TaxPolicyDto? Current,IReadOnlyList<TaxPolicyDto> Scheduled);
public sealed record MarketTaxQuoteRequest(Guid RequestId,Guid LotId);
// The buyer sees their fixed payment, not the seller's income, costs or tax bracket.
public sealed record MarketTaxQuoteDto(Guid Id,Guid LotId,long BuyerPays,long? PolicyVersion,DateTimeOffset ExpiresAt);

public sealed partial class GameService
{
    private static DateTimeOffset TaxInstant(DateTimeOffset value)=>value.ToUniversalTime().AddTicks(-(value.Ticks%10));
    private static TaxPolicyDto TaxDto(TaxPolicy p)=>new(p.Version,p.RequestId,p.Enabled,p.EffectiveAt,p.PublishedAt,
        p.Brackets.Select(b=>new TaxBracket{FromProfit=b.FromProfit,RateBasisPoints=b.RateBasisPoints}).ToArray());
    private static MarketTaxQuoteDto TaxDto(MarketTaxQuote q)=>new(q.Id,q.LotId,q.Price,q.PolicyVersion,q.ExpiresAt);

    private static Task<TaxPolicy?> ActiveTaxPolicyAsync(DesertodDbContext db,Guid village,DateTimeOffset now,CancellationToken ct)=>
        db.TaxPolicies.Where(p=>p.VillageId==village&&p.EffectiveAt<=now).OrderByDescending(p=>p.EffectiveAt).ThenByDescending(p=>p.Version).FirstOrDefaultAsync(ct);

    public async Task<TaxPolicyStateDto> GetTaxPolicyAsync(string slug,Guid actor,CancellationToken ct)=>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>{
            var village=await LoadVillageAsync(db,slug,ct);await RequireEconomyMemberAsync(db,village,actor,ct);
            var state=await db.KingdomEconomies.SingleAsync(e=>e.VillageId==village.Id,ct);var now=time.GetUtcNow();
            var current=await ActiveTaxPolicyAsync(db,village.Id,now,ct);
            var future=await db.TaxPolicies.Where(p=>p.VillageId==village.Id&&p.EffectiveAt>now).OrderBy(p=>p.EffectiveAt).ThenBy(p=>p.Version).Take(30).ToListAsync(ct);
            return new TaxPolicyStateDto(state.LatestTaxPolicyVersion,current is null?null:TaxDto(current),future.Select(TaxDto).ToArray());
        },ct);

    public async Task<TaxPolicyDto> PublishTaxPolicyAsync(string slug,Guid actor,TaxPolicyRequest request,CancellationToken ct)
    {
        if(request.RequestId==Guid.Empty||request.ExpectedVersion is null or <0||request.Enabled is null||request.EffectiveAt is null)
            throw new GameRuleException("tax.request","Supply the request identifier, current policy version, enabled flag and future effective time.");
        var candidate=new TaxPolicy{Enabled=request.Enabled.Value,Brackets=request.Brackets??[]};SaleTaxRules.ValidatePolicy(candidate);
        var fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        return await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>{
            var village=await LoadVillageAsync(db,slug,ct);if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
            return await PublishTaxPolicyCoreAsync(db,village,actor,request,candidate,fingerprint,ct);
        },ct);
    }

    public async Task<MarketTaxQuoteDto> QuoteMarketTaxAsync(string slug,Guid actor,MarketTaxQuoteRequest request,CancellationToken ct)
    {
        if(request.RequestId==Guid.Empty||request.LotId==Guid.Empty)throw new GameRuleException("tax.quote.request","Supply a quote identifier and a market lot.");
        return await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>{
            var village=await LoadVillageAsync(db,slug,ct);await RequireEconomyMemberAsync(db,village,actor,ct);
            var previous=await db.MarketTaxQuotes.SingleOrDefaultAsync(q=>q.VillageId==village.Id&&q.Id==request.RequestId,ct);
            if(previous is not null)
            {
                if(previous.BuyerAccountId!=actor||previous.LotId!=request.LotId)throw new GameRuleException("tax.quote.reused","This quote identifier already names another purchase.");
                return TaxDto(previous);
            }
            if(!await db.Citizens.AnyAsync(c=>c.VillageId==village.Id&&c.AccountId==actor,ct))
                throw new GameRuleException("economy.citizen","A resident is required for this action.");
            var lot=await db.MarketLots.SingleOrDefaultAsync(l=>l.VillageId==village.Id&&l.Id==request.LotId,ct)
                ??throw new GameRuleException("market.missing","The lot does not exist in this kingdom.");
            if(lot.Status!="open")throw new GameRuleException("market.closed","The lot is already closed.");
            if(lot.SellerAccountId==actor)throw new GameRuleException("market.own","You cannot buy your own lot.");
            var now=TaxInstant(time.GetUtcNow());var seller=await WalletAsync(db,village.Id,lot.SellerAccountId,now,ct);
            var policy=lot.TaxPolicyVersion is {} version?await db.TaxPolicies.SingleAsync(p=>p.VillageId==village.Id&&p.Version==version,ct):null;
            var tax=SaleTaxRules.Calculate(policy,lot.Price,lot.CostBasis,lot.CostBasisKnown,seller.TaxableProfitToday,seller.TaxRemainder);
            var quote=new MarketTaxQuote{VillageId=village.Id,Id=request.RequestId,LotId=lot.Id,BuyerAccountId=actor,SellerAccountId=lot.SellerAccountId,
                PolicyVersion=lot.TaxPolicyVersion,Day=EconomyDay(now),ExpectedSellerTaxRevision=seller.TaxRevision,
                Price=lot.Price,CostBasis=lot.CostBasis,CostBasisKnown=lot.CostBasisKnown,RealizedProfit=tax.RealizedProfit,
                TaxableGain=tax.TaxableGain,TaxAmount=tax.TaxAmount,RemainderAfter=tax.RemainderAfter,ExemptReason=tax.ExemptReason,
                CreatedAt=now,ExpiresAt=now+SaleTaxRules.QuoteLifetime};
            db.MarketTaxQuotes.Add(quote);await db.SaveChangesAsync(ct);return TaxDto(quote);
        },ct);
    }

    private static async Task<long> SettleMarketTaxAsync(DesertodDbContext db,MarketLot lot,Guid buyer,GameWallet seller,
        KingdomEconomy treasury,EconomyRequest request,DateTimeOffset now,CancellationToken ct)
    {
        var policy=lot.TaxPolicyVersion is {} version?await db.TaxPolicies.SingleAsync(p=>p.VillageId==lot.VillageId&&p.Version==version,ct):null;
        if(request.TaxQuoteId is null)
        {
            if(policy?.Enabled==true)throw new GameRuleException("tax.quote.required","Review a current purchase quote first.");
            return 0;
        }
        var quote=await db.MarketTaxQuotes.SingleOrDefaultAsync(q=>q.VillageId==lot.VillageId&&q.Id==request.TaxQuoteId,ct)
            ??throw new GameRuleException("tax.quote.missing","This purchase quote does not exist.");
        if(quote.BuyerAccountId!=buyer||quote.SellerAccountId!=lot.SellerAccountId||quote.LotId!=lot.Id)
            throw new GameRuleException("tax.quote.owner","This quote belongs to a different purchase.");
        if(quote.ConsumedAt is not null||quote.ExpiresAt<=now||quote.Day!=EconomyDay(now)||quote.ExpectedSellerTaxRevision!=seller.TaxRevision
            ||quote.Price!=lot.Price||quote.CostBasis!=lot.CostBasis||quote.CostBasisKnown!=lot.CostBasisKnown||quote.PolicyVersion!=lot.TaxPolicyVersion)
            throw new GameRuleException("tax.quote.stale","The quote expired or its settlement state changed. Request a fresh quote.");
        var calculated=SaleTaxRules.Calculate(policy,lot.Price,lot.CostBasis,lot.CostBasisKnown,seller.TaxableProfitToday,seller.TaxRemainder);
        if(calculated!=new SaleTaxCalculation(quote.RealizedProfit,quote.TaxableGain,quote.TaxAmount,quote.RemainderAfter,quote.ExemptReason))
            throw new GameRuleException("tax.quote.invalid","Saved settlement evidence is inconsistent.");
        if(treasury.TreasuryCoins>EconomyRules.MaximumBalance-quote.TaxAmount||seller.TaxPaidToday>long.MaxValue-quote.TaxAmount)
            throw new GameRuleException("economy.limit","The accounting balance limit has been reached.");
        var before=seller.TaxableProfitToday;
        if(quote.TaxableGain>0)
        {
            seller.TaxableProfitToday=checked(before+quote.TaxableGain);seller.TaxPaidToday+=quote.TaxAmount;
            seller.TaxRemainder=quote.RemainderAfter;seller.TaxRevision=checked(seller.TaxRevision+1);
        }
        treasury.TreasuryCoins+=quote.TaxAmount;quote.ConsumedAt=TaxInstant(now);quote.TradeRequestId=request.RequestId;
        db.MarketTaxEvents.Add(new(){VillageId=lot.VillageId,Id=request.RequestId,LotId=lot.Id,QuoteId=quote.Id,SellerAccountId=lot.SellerAccountId,
            PolicyVersion=quote.PolicyVersion,Day=quote.Day,Price=lot.Price,CostBasis=lot.CostBasis,CostBasisKnown=lot.CostBasisKnown,
            RealizedProfit=quote.RealizedProfit,TaxableGain=quote.TaxableGain,TaxAmount=quote.TaxAmount,TaxableProfitBefore=before,
            TaxableProfitAfter=seller.TaxableProfitToday,RemainderAfter=seller.TaxRemainder,ExemptReason=quote.ExemptReason,At=TaxInstant(now)});
        return quote.TaxAmount;
    }

    private async Task<TaxPolicyDto> PublishTaxPolicyCoreAsync(DesertodDbContext db, Desertod.Domain.Entities.Village village,
        Guid actor, TaxPolicyRequest request, TaxPolicy candidate, string fingerprint, CancellationToken ct)
    {
            var effectiveAt=request.EffectiveAt ?? throw new GameRuleException("tax.request","Supply a future effective time.");
            var previous=await db.TaxPolicies.SingleOrDefaultAsync(p=>p.VillageId==village.Id&&p.RequestId==request.RequestId,ct);
            if(previous is not null)
            {
                if(previous.ActorId!=actor||previous.RequestFingerprint!=fingerprint)throw new GameRuleException("tax.request.reused","This request identifier already names another policy.");
                return TaxDto(previous);
            }
            var state=await db.KingdomEconomies.SingleAsync(e=>e.VillageId==village.Id,ct);var now=time.GetUtcNow();
            if(state.LatestTaxPolicyVersion!=request.ExpectedVersion)throw new GameRuleException("tax.policy.stale","Refresh the current policy version.");
            if(effectiveAt<now)throw new GameRuleException("tax.policy.past","New rules cannot apply retroactively.");
            if(state.LatestTaxPolicyVersion==long.MaxValue)throw new GameRuleException("tax.limit","The policy version limit has been reached.");
            candidate.VillageId=village.Id;candidate.Version=++state.LatestTaxPolicyVersion;candidate.RequestId=request.RequestId;
            candidate.ActorId=actor;candidate.RequestFingerprint=fingerprint;candidate.EffectiveAt=TaxInstant(effectiveAt);candidate.PublishedAt=TaxInstant(now);
            db.TaxPolicies.Add(candidate);await db.SaveChangesAsync(ct);return TaxDto(candidate);
    }
}
