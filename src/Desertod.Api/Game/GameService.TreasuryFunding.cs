using System.Security.Cryptography;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record TreasuryFundingPolicyRequest(Guid RequestId, long? ExpectedVersion, bool? Enabled,
    DateTimeOffset? EffectiveAt, long DailyIssueLimit);
public sealed record TreasuryFundingPolicyDto(long Version, Guid RequestId, bool Enabled, long DailyIssueLimit,
    DateTimeOffset EffectiveAt, DateTimeOffset PublishedAt);
public sealed record TreasuryFundingStateDto(long LatestVersion, TreasuryFundingPolicyDto? Current,
    IReadOnlyList<TreasuryFundingPolicyDto> Scheduled, int ScheduledCount, bool ScheduledTruncated,
    DateOnly BudgetDay, long BudgetRevision, long IssuedToday, long RemainingAllowance, bool FundedWorkEnabled);
public sealed record TreasuryIssueRequest(Guid RequestId, long? PolicyVersion, DateOnly? BudgetDay, long? ExpectedBudgetRevision, long Amount);
public sealed record TreasuryIssueDto(Guid RequestId, long PolicyVersion, DateOnly Day, long Amount,
    long IssuedTotalAfter, long RemainingAtIssue, DateTimeOffset At);

public sealed partial class GameService
{
    private static TreasuryFundingPolicyDto FundingDto(TreasuryFundingPolicy p) =>
        new(p.Version, p.RequestId, p.Enabled, p.DailyIssueLimit, p.EffectiveAt, p.PublishedAt);
    private static TreasuryIssueDto FundingDto(TreasuryIssue p) =>
        new(p.RequestId, p.PolicyVersion, p.Day, p.Amount, p.IssuedTotalAfter, p.RemainingAtIssue, p.At);
    private static string FundingFingerprint<T>(T request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
    private static async Task<long> LatestFundingVersionAsync(DesertodDbContext db, Guid village, CancellationToken ct) =>
        await db.TreasuryFundingPolicies.Where(x => x.VillageId == village).MaxAsync(x => (long?)x.Version, ct) ?? 0;
    private static Task<TreasuryFundingPolicy?> ActiveFundingPolicyAsync(DesertodDbContext db, Guid village, DateTimeOffset now, CancellationToken ct) =>
        db.TreasuryFundingPolicies.Where(x => x.VillageId == village && x.EffectiveAt <= now)
            .OrderByDescending(x => x.EffectiveAt).ThenByDescending(x => x.Version).FirstOrDefaultAsync(ct);

    private static async Task<TreasuryFundingStateDto> FundingStateAsync(DesertodDbContext db, Guid village, DateTimeOffset now, CancellationToken ct)
    {
        var day = EconomyDay(now); var current = await ActiveFundingPolicyAsync(db, village, now, ct);
        var budget = await db.TreasuryIssueDays.AsNoTracking().SingleOrDefaultAsync(x => x.VillageId == village && x.Day == day, ct);
        var future = db.TreasuryFundingPolicies.AsNoTracking().Where(x => x.VillageId == village && x.EffectiveAt > now);
        var count = await future.CountAsync(ct);
        var scheduled = await future.OrderBy(x => x.EffectiveAt).ThenBy(x => x.Version).Take(30).ToListAsync(ct);
        var issued = budget?.Issued ?? 0;
        return new(await LatestFundingVersionAsync(db, village, ct), current is null ? null : FundingDto(current),
            scheduled.Select(FundingDto).ToArray(), count, count > scheduled.Count, day, budget?.Revision ?? 0, issued,
            current is { Enabled: true } ? Math.Max(0, current.DailyIssueLimit - issued) : 0,
            await db.KingdomEconomies.Where(x => x.VillageId == village).Select(x => x.WorkContractsEnabled).SingleAsync(ct));
    }

    public async Task<TreasuryFundingStateDto> GetTreasuryFundingAsync(string slug, Guid actor, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct); await RequireEconomyMemberAsync(db, village, actor, ct);
            return await FundingStateAsync(db, village.Id, time.GetUtcNow(), ct);
        }, ct);

    public async Task<TreasuryFundingPolicyDto> PublishTreasuryFundingAsync(string slug, Guid actor, TreasuryFundingPolicyRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.ExpectedVersion is null or < 0 || request.Enabled is null || request.EffectiveAt is null)
            throw new GameRuleException("funding.request", "Supply the request identifier, current version, enabled flag and future effective time.");
        TreasuryFundingRules.ValidatePolicy(request.Enabled.Value, request.DailyIssueLimit);
        var fingerprint = FundingFingerprint(request);
        return await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct); if (village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
            var prior = await db.TreasuryFundingPolicies.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.RequestId == request.RequestId, ct);
            if (prior is not null)
            {
                if (prior.ActorId != actor || prior.RequestFingerprint != fingerprint) throw new GameRuleException("funding.request.reused", "This identifier already names another funding policy.");
                return FundingDto(prior);
            }
            var latest = await LatestFundingVersionAsync(db, village.Id, ct); var now = time.GetUtcNow();
            if (latest != request.ExpectedVersion) throw new GameRuleException("funding.policy.stale", "Refresh the current funding policy version.");
            if (request.EffectiveAt.Value < now) throw new GameRuleException("funding.policy.past", "New funding rules cannot apply retroactively.");
            if (latest == long.MaxValue) throw new GameRuleException("funding.limit", "The funding policy version limit has been reached.");
            var policy = new TreasuryFundingPolicy { VillageId = village.Id, Version = latest + 1, RequestId = request.RequestId,
                ActorId = actor, RequestFingerprint = fingerprint, Enabled = request.Enabled.Value, DailyIssueLimit = request.DailyIssueLimit,
                PublishedAt = TaxInstant(now), EffectiveAt = TaxInstant(request.EffectiveAt.Value) };
            db.TreasuryFundingPolicies.Add(policy); await db.SaveChangesAsync(ct); changes.Mark(village.Id);
            return FundingDto(policy);
        }, ct);
    }

    public async Task<TreasuryIssueDto> IssueTreasuryAsync(string slug, Guid actor, TreasuryIssueRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.PolicyVersion is null or < 1 || request.BudgetDay is null
            || request.ExpectedBudgetRevision is null or < 0 || request.Amount < 1)
            throw new GameRuleException("funding.request", "Confirm the amount, current policy, budget day and revision.");
        var fingerprint = FundingFingerprint(request);
        return await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct); if (village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
            return await IssueTreasuryCoreAsync(db, village, actor, request, fingerprint, ct);
        }, ct);
    }

    private async Task<TreasuryIssueDto> IssueTreasuryCoreAsync(DesertodDbContext db, Desertod.Domain.Entities.Village village,
        Guid actor, TreasuryIssueRequest request, string fingerprint, CancellationToken ct)
    {
            var prior = await db.TreasuryIssues.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.RequestId == request.RequestId, ct);
            if (prior is not null)
            {
                if (prior.ActorId != actor || prior.RequestFingerprint != fingerprint) throw new GameRuleException("funding.request.reused", "This identifier already names another treasury issue.");
                return FundingDto(prior);
            }
            var now = TaxInstant(time.GetUtcNow()); var day = EconomyDay(now);
            var policy = await ActiveFundingPolicyAsync(db, village.Id, now, ct);
            if (policy is null || !policy.Enabled) throw new GameRuleException("funding.disabled", "New treasury issuance is disabled.");
            if (policy.Version != request.PolicyVersion) throw new GameRuleException("funding.policy.stale", "Refresh the active funding policy.");
            if (request.BudgetDay != day) throw new GameRuleException("funding.day.stale", "The daily allowance changed. Refresh the funding state.");
            var economy = await db.KingdomEconomies.SingleAsync(x => x.VillageId == village.Id, ct);
            if (!economy.WorkContractsEnabled) throw new GameRuleException("funding.workMode", "Enable funded public work before using treasury issuance.");
            var budget = await db.TreasuryIssueDays.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.Day == day, ct);
            if ((budget?.Revision ?? 0) != request.ExpectedBudgetRevision) throw new GameRuleException("funding.budget.stale", "The allowance changed. Refresh the funding state.");
            if (budget is null) { budget = new() { VillageId = village.Id, Day = day }; db.TreasuryIssueDays.Add(budget); }
            economy.TreasuryCoins = TreasuryFundingRules.Issue(policy, budget, request.Amount, economy.TreasuryCoins);
            var receipt = new TreasuryIssue { VillageId = village.Id, RequestId = request.RequestId, ActorId = actor,
                RequestFingerprint = fingerprint, PolicyVersion = policy.Version, Day = day, Amount = request.Amount,
                IssuedTotalAfter = budget.Issued, RemainingAtIssue = policy.DailyIssueLimit - budget.Issued, At = now,
                FinancialBatchId = db.FinancialBatchId ?? throw MonetaryAudit.Invalid() };
            db.TreasuryIssues.Add(receipt);
            MoneyEntry(db, village.Id, request.RequestId, actor, "treasury-issuance", "DZK", "treasury", request.Amount, now);
            await db.SaveChangesAsync(ct); changes.Mark(village.Id); return FundingDto(receipt);
    }
}
