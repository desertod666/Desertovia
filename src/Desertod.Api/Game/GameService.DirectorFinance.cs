using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Director;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record DirectorFinancePolicyRequest(Guid RequestId, long? ExpectedVersion, DirectorFinanceLimits? Limits);
public sealed record DirectorFinanceView(long Version, DirectorFinanceLimits Limits, DateOnly Day, int ActionsToday,
    long ReservedToday, IReadOnlyList<DirectorFinanceAction> Actions);
internal sealed record FinanceCommand(string Option, string Kind, long Cost = 0, string? Task = null,
    ResourceKind Resource = ResourceKind.Wood, int Units = 0, long UnitPay = 0, long Issue = 0,
    bool TaxEnabled = false, List<TaxBracket>? Brackets = null);
internal sealed record FinanceFunding(long? PolicyVersion, DateOnly BudgetDay, long BudgetRevision, long RemainingAllowance);
internal sealed record FinanceEnvelope(DateTimeOffset CapturedAt, string Revision, long PolicyVersion,
    DateOnly Day, long BudgetRevision, long TaxVersion, FinanceFunding Funding,
    IReadOnlyList<FinanceCommand> Commands, object Evidence, object Consequences);

public sealed partial class GameService
{
    private static Task<DirectorFinancePolicy?> FinancePolicyAsync(DesertodDbContext db, Guid village, CancellationToken ct) =>
        db.DirectorFinancePolicies.Where(x => x.VillageId == village).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);

    public async Task<DirectorFinanceView> GetDirectorFinanceAsync(string slug, Guid actor, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct); if (village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
            var policy = await FinancePolicyAsync(db, village.Id, ct); var day = EconomyDay(time.GetUtcNow());
            var budget = await db.DirectorFinanceDays.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.Day == day, ct);
            return new DirectorFinanceView(policy?.Version ?? 0, policy?.Limits ?? new(), day, budget?.Actions ?? 0, budget?.Reserved ?? 0,
                await db.DirectorFinanceActions.AsNoTracking().Where(x => x.VillageId == village.Id)
                    .OrderByDescending(x => x.At).ThenByDescending(x => x.ReviewId).Take(30).ToArrayAsync(ct));
        }, ct);

    public async Task<long> SetDirectorFinanceAsync(string slug, Guid actor, DirectorFinancePolicyRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.ExpectedVersion is null or < 0 || request.Limits is null)
            throw new GameRuleException("director.finance.request", "Confirm the delegation version and explicit limits.");
        DirectorFinanceRules.Validate(request.Limits); var fingerprint = FundingFingerprint(request);
        return await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct); if (village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
            var prior = await db.DirectorFinancePolicies.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.RequestId == request.RequestId, ct);
            if (prior is not null)
            {
                if (prior.OwnerAccountId != actor || prior.Fingerprint != fingerprint)
                    throw new GameRuleException("director.finance.reused", "This request already names another delegation.");
                return prior.Version;
            }
            var latest = await FinancePolicyAsync(db, village.Id, ct);
            if ((latest?.Version ?? 0) != request.ExpectedVersion || latest?.Version == long.MaxValue)
                throw new GameRuleException("director.finance.stale", "Refresh the delegation version.");
            var now = TaxInstant(time.GetUtcNow()); var version = (latest?.Version ?? 0) + 1;
            db.DirectorFinancePolicies.Add(new() { VillageId = village.Id, Version = version, OwnerAccountId = actor,
                RequestId = request.RequestId, Fingerprint = fingerprint, Limits = request.Limits, At = now });
            var state = await DirectorStateAsync(db, village.Id, now, ct);
            state.Lease = null; state.LeaseUntil = null; state.NextReviewAt = now; state.PolicyVersion++; state.Version++;
            await db.SaveChangesAsync(ct); changes.Mark(village.Id); return version;
        }, ct);
    }

    private async Task<FinanceEnvelope?> FinanceEnvelopeAsync(DesertodDbContext db, Village village, AlmightyState state,
        DateTimeOffset capturedAt, DateTimeOffset now, CancellationToken ct)
    {
        if (state.Mode != "AutoEconomy" || state.DisabledKinds.Contains("economy")) return null;
        var policy = await FinancePolicyAsync(db, village.Id, ct);
        if (policy is null || !policy.Limits.Enabled || policy.OwnerAccountId != village.OwnerAccountId) return null;
        var limits = policy.Limits; DirectorFinanceRules.Validate(limits);
        var day = EconomyDay(now); var budget = await db.DirectorFinanceDays.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.Day == day, ct);
        var economy = await db.KingdomEconomies.SingleAsync(x => x.VillageId == village.Id, ct);
        var funding = await FundingStateAsync(db, village.Id, now, ct);
        var tax = await ActiveTaxPolicyAsync(db, village.Id, now, ct);
        var futureTaxes = await db.TaxPolicies.Where(x => x.VillageId == village.Id && x.EffectiveAt > now)
            .OrderBy(x => x.Version).Select(x => new { x.Version, x.EffectiveAt }).ToArrayAsync(ct);
        var works = await db.WorkOrders.Where(x => x.VillageId == village.Id && x.Status == WorkOrderStatus.Open && x.ExpiresAt > now)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.TaskId, x.AvailableCycles, x.AcceptedCycles, x.ReservedCoins, x.PayPerCycle }).ToArrayAsync(ct);
        var supplies = await db.SupplyOrders.Where(x => x.VillageId == village.Id && x.Status == SupplyOrderStatus.Open && x.ExpiresAt > now)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Resource, x.RemainingAmount, x.ReservedCoins }).ToArrayAsync(ct);
        var residents = await db.Citizens.Where(x => x.VillageId == village.Id && x.Status == CitizenStatus.Active)
            .Select(x => new { x.IsHungry }).ToArrayAsync(ct);
        var stocks = Enum.GetValues<ResourceKind>().Where(StorageCatalog.IsGood).Select(kind => new {
            resource = kind, stock = TradeReserveRules.PlainPublicStock(village, kind), capacity = village.FreeSpace(kind),
            target = Math.Max(economy.TradeReserves.SingleOrDefault(x => x.Resource == kind)?.MinimumAmount ?? 0,
                kind == ResourceKind.Food ? residents.Count(x => x.IsHungry) : 0) }).OrderBy(x => x.resource).ToArray();
        var priorActions = await db.DirectorFinanceActions.AsNoTracking().Where(x => x.VillageId == village.Id)
            .OrderByDescending(x => x.At).ThenByDescending(x => x.ReviewId).Take(3).ToArrayAsync(ct);
        var lastTax = await db.DirectorFinanceActions.Where(x => x.VillageId == village.Id && x.Option.StartsWith("finance_tax_"))
            .MaxAsync(x => (DateTimeOffset?)x.At, ct);
        var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-14);
        // Use complete actor-days with known costs; opening wealth and unknown historical basis never become profit.
        var firstLedger = await db.LedgerEntries.Where(x => x.VillageId == village.Id).MinAsync(x => (DateTimeOffset?)x.At, ct);
        var coverage = firstLedger > economy.StartedAt ? firstLedger.Value : economy.StartedAt;
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var marketProfits = await db.MarketLots.Where(x => x.VillageId == village.Id && x.Status == "sold" && x.CostBasisKnown
                && x.ClosedAt >= start && x.ClosedAt < today && x.ClosedAt >= coverage && x.Price > x.CostBasis)
            .GroupBy(x => new { Day = DateOnly.FromDateTime(x.ClosedAt!.Value.UtcDateTime), Account = x.SellerAccountId })
            .Select(g => new { g.Key.Day, g.Key.Account, Profit = g.Sum(x => (decimal)x.Price - x.CostBasis) }).ToArrayAsync(ct);
        var supplyProfits = await db.SupplyDeliveries.Where(x => x.VillageId == village.Id && x.CostBasisKnown
                && x.At >= start && x.At < today && x.At >= coverage && x.PaidCoins > x.CostBasis)
            .GroupBy(x => new { Day = DateOnly.FromDateTime(x.At.UtcDateTime), Account = x.SupplierAccountId })
            .Select(g => new { g.Key.Day, g.Key.Account, Profit = g.Sum(x => (decimal)x.PaidCoins - x.CostBasis) }).ToArrayAsync(ct);
        var profit = marketProfits.Concat(supplyProfits).Where(x => new DateTimeOffset(x.Day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) >= coverage)
            .GroupBy(x => new { x.Day, x.Account }).Select(g => g.Sum(x => x.Profit)).Order().ToArray();
        var medianProfit = EconomicDistribution(profit).MedianAmongRecipients;
        var payHistory = await db.LedgerEntries.Where(x => x.VillageId == village.Id && x.Asset == "DZK" && x.Location == "wallet"
                && x.At >= start && x.At <= now && x.Delta > 0 && (x.Operation == "wage" || x.Operation == "contract-wage"))
            .GroupBy(x => x.Delta).Select(g => new { Pay = g.Key, Count = g.Count() }).OrderBy(x => x.Pay).ToArrayAsync(ct);
        long observedPay = 0; var halfway = (payHistory.Sum(x => (long)x.Count) + 1) / 2; long seen = 0;
        foreach (var row in payHistory) { seen += row.Count; if (halfway > 0 && seen >= halfway) { observedPay = row.Pay; break; } }
        var sample = new { treasury = economy.TreasuryCoins, food = stocks.Single(x => x.resource == ResourceKind.Food).stock,
            hungryResidents = residents.Count(x => x.IsHungry), knownProfitCompleteActorDays = profit.Length, medianKnownProfit = medianProfit,
            observedPayPerCompletedCycle = observedPay, workReserved = works.Sum(x => x.ReservedCoins), supplyReserved = supplies.Sum(x => x.ReservedCoins) };
        var input = new { policy.Version, limits, owner = village.OwnerAccountId, state.Mode, state.PolicyVersion, state.DisabledKinds,
            day, actions = budget?.Actions ?? 0, reserved = budget?.Reserved ?? 0, revision = budget?.Revision ?? 0,
            economy.WorkContractsEnabled, economy.LatestTaxPolicyVersion, funding, tax = tax is null ? null : TaxDto(tax), futureTaxes,
            stocks, works, supplies, sample, lastTax };
        var lastPosting=await db.LedgerEntries.Where(x=>x.VillageId==village.Id).OrderByDescending(x=>x.At).ThenByDescending(x=>x.Id)
            .Select(x=>new{x.Id,x.At}).FirstOrDefaultAsync(ct);
        var revision = FundingFingerprint(new{input,lastPosting,moneyHash=HoldingHash(await SnapshotMoneyAsync(db,village.Id,ct))});
        var commands = new List<FinanceCommand>();
        var room = Math.Max(0, limits.SpendingPerDay - (budget?.Reserved ?? 0));
        if ((budget?.Actions ?? 0) < limits.ActionsPerDay)
        {
            // Subtract already ordered output so repeated reviews do not fund the same shortage again.
            var shortages = stocks.Select(s => new { s.resource, s.capacity, deficit = Math.Max(0, s.target - s.stock
                - supplies.Where(x => x.Resource == s.resource).Sum(x => x.RemainingAmount)
                - works.Sum(w => (GameCatalog.FindTask(w.TaskId)?.Outputs.Where(o => o.Resource == s.resource).Sum(o => o.Amount) ?? 0)
                    * (w.AvailableCycles + w.AcceptedCycles))) }).Where(x => x.deficit > 0 && x.capacity > 0)
                .OrderByDescending(x => x.resource == ResourceKind.Food).ThenByDescending(x => x.deficit).ThenBy(x => x.resource).ToArray();
            var offers = new List<FinanceCommand>();
            foreach (var shortage in shortages.Take(3))
            {
                var task = GameCatalog.Tasks.FirstOrDefault(t => WorkOrderRules.Supports(t) && t.Inputs.Count == 0 && t.Outputs.Any(o => o.Resource == shortage.resource));
                if (limits.WorkOrders && economy.WorkContractsEnabled && task is not null && works.Length < 50
                    && !offers.Any(x => x.Kind == "work"))
                {
                    var output = task.Outputs.Where(x => x.Resource == shortage.resource).Sum(x => x.Amount);
                    var units = Math.Min(limits.MaximumUnits, (Math.Min(shortage.deficit, shortage.capacity) + output - 1) / output);
                    AddOffers("work", task.Id, shortage.resource, units, observedPay > 0 ? observedPay : Math.Max(1, limits.MaximumUnitPay / 2));
                }
                if (limits.SupplyOrders && supplies.Length < 50 && !offers.Any(x => x.Kind == "supply"))
                    AddOffers("supply", null, shortage.resource, Math.Min(limits.MaximumUnits, Math.Min(shortage.deficit, shortage.capacity)), EconomyRules.StorePrice(shortage.resource));
            }
            void AddOffers(string kind, string? task, ResourceKind resource, int units, long basis)
            {
                var values = new[] { Math.Max(1, basis / 2), basis, basis + Math.Max(1, basis / 2) };
                var names = new[] { "low", "mid", "high" }; var used = new HashSet<long>();
                for (var i = 0; i < 3; i++)
                {
                    var pay = Math.Clamp(values[i], 1, limits.MaximumUnitPay);
                    if (!used.Add(pay) || units < 1) continue;
                    var affordableUnits = (int)Math.Min(units, room / pay); if (affordableUnits < 1) continue;
                    offers.Add(new("finance_" + kind + "_" + names[i], kind, affordableUnits * pay, task, resource, affordableUnits, pay));
                }
            }
            commands.AddRange(offers.Where(x => x.Cost <= economy.TreasuryCoins));
            var missing = offers.Count == 0 ? 0 : Math.Max(0, offers.Min(x => x.Cost) - economy.TreasuryCoins);
            if (limits.TreasuryIssues && economy.WorkContractsEnabled && missing > 0 && funding.RemainingAllowance > 0)
                commands.Add(new("finance_issue", "issue", Issue: Math.Min(missing, Math.Min(limits.MaximumIssue, funding.RemainingAllowance))));
            if (limits.TaxChanges && futureTaxes.Length == 0 && (lastTax is null || lastTax <= now.AddMinutes(-limits.TaxCooldownMinutes)))
            {
                if (tax is { Enabled: true } && tax.Brackets.Any(x => x.RateBasisPoints > 0))
                {
                    var down = tax.Brackets.Select(b => new TaxBracket { FromProfit = b.FromProfit, RateBasisPoints = Math.Min(limits.MaximumTaxBasisPoints, Math.Max(0, b.RateBasisPoints - limits.TaxStepBasisPoints)) }).ToList();
                    if (tax.Brackets.Any(b => b.RateBasisPoints > 0)) commands.Add(new("finance_tax_down", "tax", TaxEnabled: down.Any(b => b.RateBasisPoints > 0), Brackets: down.Any(b => b.RateBasisPoints > 0) ? down : []));
                    if (medianProfit > 0)
                    {
                        var up = tax.Brackets.Select(b => new TaxBracket { FromProfit = b.FromProfit,
                            RateBasisPoints = b.FromProfit == 0 && b.RateBasisPoints == 0 ? 0 : Math.Min(limits.MaximumTaxBasisPoints, b.RateBasisPoints + limits.TaxStepBasisPoints) }).ToList();
                        if (up.Zip(tax.Brackets).Any(p => p.First.RateBasisPoints > p.Second.RateBasisPoints)
                            && up.Zip(tax.Brackets).All(p => p.First.RateBasisPoints >= p.Second.RateBasisPoints))
                            commands.Add(new("finance_tax_up", "tax", TaxEnabled: true, Brackets: up));
                    }
                }
                else if (medianProfit > 0)
                    commands.Add(new("finance_tax_up", "tax", TaxEnabled: true, Brackets: [new() { FromProfit = 0, RateBasisPoints = 0 },
                        new() { FromProfit = (long)Math.Min(EconomyRules.MaximumBalance, Math.Max(1, medianProfit)), RateBasisPoints = limits.TaxStepBasisPoints }]));
            }
        }
        var consequences = new { causalAttributionProven = false, current = sample,
            previous = priorActions.Select(x => new { x.At, x.Option, x.Result, before = JsonSerializer.Deserialize<JsonElement>(x.Before),
                reason = x.Reason.Length <= 200 ? x.Reason : x.Reason[..200] }),
            interpretation = "Compare before/current income, hunger and commitments in economy.analysis. Changes after a decision alone do not prove causation; do not infer labor hours from wages." };
        return new(capturedAt, revision, policy.Version, day, budget?.Revision ?? 0, economy.LatestTaxPolicyVersion,
            new(funding.Current?.Version, funding.BudgetDay, funding.BudgetRevision, funding.RemainingAllowance), commands,
            new { limits, sample, completeProfitMedianIsNotWage = true, noCompletedWageEvidence = observedPay == 0,
                freeProfitThresholdSource = "median positive known-profit actor-day over covered complete UTC days; unknown costs and zero profits excluded; known zero monetary cost retained",
                targets = stocks.Where(x => x.target > 0).Take(8), targetCount = stocks.Count(x => x.target > 0),
                shortagesUseOwnerReservesAndHungryResidents = true }, consequences);
    }

    private static AlmightyOption FinanceOption(FinanceCommand command) => new(command.Option, command.Kind switch
    {
        "work" => $"Заказ: {command.Task}, {command.Units} циклов по {command.UnitPay}",
        "supply" => $"Закупка: {ResourceNames.Of(command.Resource)}, {command.Units} по {command.UnitPay}",
        "issue" => $"Подкрепить казну: {command.Issue}",
        _ => command.Option == "finance_tax_up" ? "Изменить будущую дань: шаг вверх" : "Изменить будущую дань: шаг вниз"
    }, command.Kind == "tax" ? "Новая объявленная версия после срока уведомления; старые сделки сохраняют условия."
        : command.Kind == "issue" ? "Выпуск только из объявленного суточного лимита под наблюдаемую нехватку фонда."
        : $"Заранее зарезервировать {command.Cost} из казны; принятый труд сохраняет оплату при отключении директора.");

    private static async Task OfferDueRaidAfterFinanceAsync(DesertodDbContext db, AlmightyState state, AlmightyObservation observation,
        JsonElement snapshot, IReadOnlyList<AlmightyOption> options, DateTimeOffset now, CancellationToken ct)
    {
        // Economic duties cannot starve the existing threat rhythm. This is still an explicit owner proposal.
        if (state.LastNoticeAt > now.AddMinutes(-30) || !snapshot.TryGetProperty("raidPressure", out var pressure)
            || !pressure.GetProperty("proposalDue").GetBoolean()
            || !pressure.TryGetProperty("fallbackOption", out var fallback) || fallback.ValueKind != JsonValueKind.String
            || options.FirstOrDefault(x => x.Id == fallback.GetString() && x.Enemy is not null) is not { } raid) return;
        var ids = AlmightyPolicy.RaidOptionIds();
        if (await db.AlmightyProposals.AnyAsync(x => x.VillageId == observation.VillageId && ids.Contains(x.Option)
            && (x.CreatedAt > now - AlmightyPolicy.RaidProposalInterval || x.Status == "Pending" && x.ExpiresAt > now), ct)) return;
        db.AlmightyProposals.Add(new() { VillageId = observation.VillageId, ReviewId = observation.Id, Option = raid.Id,
            Reason = "Планировщик угроз: экономическое действие Всевышнего выполнено отдельно. После паузы пора предложить ограниченную волну. Без согласия царя нападение не начнётся.",
            Intent = state.Intent, CreatedAt = now, ExpiresAt = now + AlmightyPolicy.ProposalLifetime, Snapshot = observation.Snapshot });
        state.LastNoticeAt = now;
    }

    private async Task<bool> ExecuteDirectorFinanceAsync(DesertodDbContext db, AlmightyState state, AlmightyObservation observation,
        JsonElement saved, string option, string reason, DateTimeOffset now, CancellationToken ct)
    {
        if (!saved.TryGetProperty("finance", out var json) || json.ValueKind != JsonValueKind.Object) return false;
        FinanceEnvelope? admitted;
        try { admitted=json.Deserialize<FinanceEnvelope>(AlmightyJson); }
        catch(JsonException) { return false; }
        if (admitted is null || admitted.CapturedAt > now || now - admitted.CapturedAt > TimeSpan.FromMinutes(2)) return false;
        var village = await db.Villages.SingleAsync(x => x.Id == observation.VillageId, ct);
        var fresh = await FinanceEnvelopeAsync(db, village, state, admitted.CapturedAt, now, ct);
        var command = fresh?.Commands.SingleOrDefault(x => x.Option == option);
        // The stored payload and fresh payload must both match; a stale model answer is never silently resized.
        if (fresh is null || command is null || fresh.Revision != admitted.Revision || fresh.PolicyVersion != admitted.PolicyVersion
            || FundingFingerprint(fresh.Commands) != FundingFingerprint(admitted.Commands)) return false;
        var policy = (await FinancePolicyAsync(db, village.Id, ct))!;
        var day = await db.DirectorFinanceDays.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.Day == fresh.Day, ct);
        if (day is null) { day = new() { VillageId = village.Id, Day = fresh.Day }; db.DirectorFinanceDays.Add(day); }
        DirectorFinanceRules.Reserve(policy.Limits, day, command.Cost);
        var action = new DirectorFinanceAction { VillageId = village.Id, ReviewId = observation.Id, PolicyVersion = policy.Version,
            Day = fresh.Day, DelegatingOwnerId = village.OwnerAccountId, Option = option, Terms = JsonSerializer.Serialize(command, AlmightyJson),
            Reason = reason, Intent = state.Intent, Before = JsonSerializer.Serialize(fresh.Evidence, AlmightyJson), At = TaxInstant(now) };
        db.DirectorFinanceActions.Add(action); db.DirectorReviewId = observation.Id;
        var economy = await db.KingdomEconomies.SingleAsync(x => x.VillageId == village.Id, ct);
        var request = new EconomyRequest(observation.Id, command.Kind == "work" ? "publish-work-order" : "publish-supply-order",
            command.Resource, command.Units, command.UnitPay, TaskId: command.Task, Cycles: command.Units, Pay: command.UnitPay,
            ExpiresAt: now.AddDays(1));
        // The funding account is the delegated owner's kingdom, while the durable author/ledger is the director.
        var unusedWallet = new GameWallet { VillageId = village.Id, AccountId = village.OwnerAccountId };
        switch (command.Kind)
        {
            case "work":
                await ChangeWorkOrderAsync(db, village, economy, unusedWallet, village.OwnerAccountId, request, now, ct);
                action.Result = "Зарезервирован фонд работы: " + command.Cost; break;
            case "supply":
                await ChangeSupplyOrderAsync(db, village, economy, unusedWallet, village.OwnerAccountId, request, null, now, ct);
                action.Result = "Зарезервирован фонд закупки: " + command.Cost; break;
            case "issue":
                var issue = new TreasuryIssueRequest(observation.Id, fresh.Funding.PolicyVersion, fresh.Day, fresh.Funding.BudgetRevision, command.Issue);
                await IssueTreasuryCoreAsync(db, village, Guid.Empty, issue, FundingFingerprint(issue), ct);
                action.Result = "Выпущено в казну: " + command.Issue; break;
            case "tax":
                var taxRequest = new TaxPolicyRequest(observation.Id, fresh.TaxVersion, command.TaxEnabled,
                    now.AddMinutes(policy.Limits.TaxNoticeMinutes), command.Brackets ?? []);
                var candidate = new TaxPolicy { Enabled = command.TaxEnabled, Brackets = command.Brackets ?? [] }; SaleTaxRules.ValidatePolicy(candidate);
                var result = await PublishTaxPolicyCoreAsync(db, village, Guid.Empty, taxRequest, candidate, FundingFingerprint(taxRequest), ct);
                action.Result = $"Объявлена дань v{result.Version} с {result.EffectiveAt:O}"; break;
            default: throw new GameRuleException("director.finance.command", "Unknown economic command.");
        }
        observation.Reason = reason + " · " + action.Result; changes.Mark(village.Id);
        await db.SaveChangesAsync(ct); return true;
    }
}
