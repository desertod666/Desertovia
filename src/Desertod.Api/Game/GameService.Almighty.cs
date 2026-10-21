using Desertod.Domain.Rules;
using Desertod.Domain.World;
using Desertod.Domain.Dungeons;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Director;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Chat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record AlmightyView(AlmightyState State, IReadOnlyList<AlmightyProposal> Proposals,
    IReadOnlyList<AlmightyObservation> Observations, bool Enabled);
public sealed record AlmightyReview(Guid VillageId, string Slug, Guid Lease, string Snapshot, string Intent,
    IReadOnlyList<AlmightyOption> Options);
public sealed record AlmightyAnswer(string Option, string Reason, string Intent, string? ErrorCode = null);
public sealed record AlmightyVote(bool Accept);
public sealed record AlmightyMode(string Mode);
public sealed record AlmightyNotice(Guid ProposalId, string Slug, string OwnerId, string Title, string Reason, string Effect, DateTimeOffset ExpiresAt);

public sealed partial class GameService
{
    internal static readonly JsonSerializerOptions AlmightyJson = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All) };

    private async Task<AlmightyState> DirectorStateAsync(DesertodDbContext db, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var state = await db.AlmightyStates.SingleOrDefaultAsync(s => s.VillageId == id, ct);
        if (state is not null) return state;
        state = new() { VillageId = id, NextReviewAt = now, BudgetDay = new(now.UtcDateTime.Date, TimeSpan.Zero) };
        db.AlmightyStates.Add(state);
        return state;
    }

    private async Task<AlmightyFacts> DirectorFactsAsync(DesertodDbContext db, Village village, AlmightyState state, DateTimeOffset now, CancellationToken ct)
    {
        var citizens = await db.Citizens.Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active).ToListAsync(ct);
        bool Ready(Citizen c) => !CombatRules.IsInjured(c, now) && !(c.Recovery?.Until > now) && c.RecoveryPausedAt is null && c.Recovery?.Available != false && !(c.ExhaustedUntil > now);
        var online = citizens.Where(c => presence.IsPresent(c.Id)).ToList();
        return new(citizens.Count, online.Count,
            online.Count(c => RaidSimulation.CanFight(c,now)),
            citizens.Count(c => !Ready(c)), village.Raids.Count(r => r.IsPending), village.Sites.Count,
            village.Structures.Count(s => s.Damage > 0) + village.Storages.Count(s => s.Damage > 0),
            village.Raids.Count(r => r.Outcome == RaidOutcome.Lost && (r.HeavyDefeat ?? (r.Injured >= 2 || village.DefeatedAt is not null)) && r.ResolvedAt >= now.AddHours(-2)),
            CombatRules.IsPeaceful(village, now), state.RestUntil > now || village.Raids.Any(r => r.AnnouncedAt > now - AlmightyPolicy.RaidQuietInterval),
            village.DefeatedAt is not null);
    }

    public async Task<AlmightyView> GetAlmightyAsync(string slug, Guid actor, bool enabled, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db => {
            var village = await LoadVillageAsync(db, slug, ct);
            if (village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
            var state = await DirectorStateAsync(db, village.Id, time.GetUtcNow(), ct);
            await db.SaveChangesAsync(ct);
            return new AlmightyView(state,
                await db.AlmightyProposals.Where(p => p.VillageId == village.Id).OrderByDescending(p => p.CreatedAt).Take(30).ToListAsync(ct),
                await db.AlmightyObservations.Where(p => p.VillageId == village.Id).OrderByDescending(p => p.At).Take(12).ToListAsync(ct), enabled);
        }, ct);

    public async Task SetAlmightyModeAsync(string slug, Guid actor, string mode, CancellationToken ct)
    {
        if (!AlmightyPolicy.IsMode(mode)) throw new GameRuleException("director.mode", "Неизвестный режим Всевышнего.");
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db => {
            var village = await LoadVillageAsync(db, slug, ct);
            if (village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
            var state = await DirectorStateAsync(db, village.Id, time.GetUtcNow(), ct);
            state.Mode = mode; state.Version++; state.Lease = null; state.LeaseUntil = null;
            state.NextReviewAt = time.GetUtcNow();
            foreach (var proposal in await db.AlmightyProposals.Where(p => p.VillageId == village.Id && p.Status == "Pending").ToListAsync(ct)) proposal.Status = "Superseded";
            await db.SaveChangesAsync(ct); return true;
        }, ct);
    }

    public Task<AlmightyReview?> BeginAlmightyReviewAsync(Guid villageId, CancellationToken ct) =>
        WithVillageAsync<AlmightyReview?>(villageId, async db => {
            var now = time.GetUtcNow();
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, ct);
            var state = await DirectorStateAsync(db, villageId, now, ct);
            if (state.Mode == "Off" || state.LeaseUntil > now) { await db.SaveChangesAsync(ct); return null; }
            var facts = await DirectorFactsAsync(db, village, state, now, ct);
            var lastRaid = village.Raids.Where(r => !r.IsPending).OrderByDescending(r => r.ResolvedAt).FirstOrDefault();
            var signal = $"{lastRaid?.Id}:{facts.Patients}:{village.Structures.Count}:{facts.BuildingSites}";
            var recent = await db.ChatMessages.Where(m => m.VillageId == villageId && m.SentAt >= now.AddMinutes(-10))
                .OrderByDescending(m => m.SentAt).Take(20).ToListAsync(ct);
            var distinctAuthors = recent.Select(m => m.AccountId).Distinct().Count();
            var important = signal != state.LastSignal || distinctAuthors >= 3 && recent.Any(m => m.SentAt > (state.LastReviewAt ?? DateTimeOffset.MinValue));
            if (state.NextReviewAt > now && !(important && state.LastReviewAt <= now - AlmightyPolicy.ImportantInterval)) { await db.SaveChangesAsync(ct); return null; }
            var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            if (state.BudgetDay != day) { state.BudgetDay = day; state.CallsToday = 0; }
            if (state.CallsToday >= AlmightyPolicy.DailyModelLimit) { state.NextReviewAt = day.AddDays(1); await db.SaveChangesAsync(ct); return null; }
            var hourly = await db.AlmightyObservations.CountAsync(o => o.VillageId == villageId && o.At > now.AddHours(-1), ct);
            if (hourly >= 4) { state.NextReviewAt = now.AddMinutes(15); await db.SaveChangesAsync(ct); return null; }
            var guardCitizens = await db.Citizens.Where(c => c.VillageId == villageId && c.Status == CitizenStatus.Active).ToListAsync(ct);
            var projects = await db.VillageProjects.Where(p => p.VillageId == villageId && p.Status == Desertod.Domain.Projects.ProjectStatus.Completed).Select(p => p.DefinitionId).ToListAsync(ct);
            Fortifications.Ensure(village,projects);
            var readiness=RaidBalance.Assess(village,guardCitizens,online: guardCitizens.Where(c=>presence.IsPresent(c.Id)).Select(c=>c.Id).ToHashSet(),now);
            var options = AlmightyPolicy.Options(facts, RaidBalance.Approaches(village),readiness.Budget).Concat(VigorTuning.Options(village)).ToList();
            if (facts.Online >= 3 && facts.PendingRaids == 0 && facts.Patients == 0 && !await db.DungeonRuns.AnyAsync(r => r.VillageId == villageId && r.Kind==DungeonRunKind.Group && r.Phase != DungeonPhase.Ended, ct) && !await HasPersonalEncounterAsync(db,villageId,village.OwnerAccountId,ct) && RegionalTravel.WesternExit(village) is not null)
                options.Add(new("dungeon", "Общий поход", "Открыть сбор в существующий данж. Вход добровольный, обычные правила/награды/ранения."));
            // Любая недавняя заявка о набеге, включая отклонённую, даёт царю паузу.
            var raidOptionIds = AlmightyPolicy.RaidOptionIds();
            var lastRaidProposal = await db.AlmightyProposals.Where(p => p.VillageId == villageId && raidOptionIds.Contains(p.Option))
                .MaxAsync(p => (DateTimeOffset?)p.CreatedAt, ct);
            var lastEncounter = village.Raids.Select(r => r.ResolvedAt ?? r.AnnouncedAt)
                .Append(village.CreatedAt).Max();
            var raidPressure = AlmightyPolicy.RaidPressure(facts, now, lastEncounter, lastRaidProposal);
            var fallbackRaid = raidPressure.ProposalDue ? options.FirstOrDefault(o => o.Enemy is not null && o.Composition == "mixed")
                ?? options.FirstOrDefault(o => o.Enemy is not null) : null;
            var stock = village.Storages.SelectMany(s => s.Goods).GroupBy(g => g.Kind.ToString()).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var previous = await db.AlmightyProposals.Where(p => p.VillageId == villageId).OrderByDescending(p => p.CreatedAt)
                .Take(4).Select(p => new { p.Option, p.Status, p.Reason, p.Result }).ToListAsync(ct);
            var priorObservation = await db.AlmightyObservations.AsNoTracking().Where(o => o.VillageId == villageId && o.At <= now.AddMinutes(-15)).OrderByDescending(o => o.At).FirstOrDefaultAsync(ct);
            object? previousStock = null;
            if (priorObservation is not null) {
                using var saved = JsonDocument.Parse(priorObservation.Snapshot);
                if(saved.RootElement.TryGetProperty("stock",out var oldStock)) previousStock = new { priorObservation.At, stock = oldStock.Clone() };
            }
            var accounting = await db.KingdomEconomies.AsNoTracking().SingleOrDefaultAsync(x => x.VillageId == villageId, ct);
            var economicChanges = await db.LedgerEntries.AsNoTracking().Where(x => x.VillageId == villageId && x.At >= now.AddHours(-6) && x.Operation != "opening")
                .GroupBy(x => new { x.Asset, x.Operation }).Select(g => new { g.Key.Asset, g.Key.Operation, delta = g.Sum(x => x.Delta), entries = g.Count() }).Take(20).ToListAsync(ct);
            var incomeDay = EconomyDay(now);
            var wagesToday = await db.GameWallets.Where(w=>w.VillageId==villageId && w.IncomeDay==incomeDay).SumAsync(w=>(long?)w.EarnedToday,ct) ?? 0;
            var tradingProfitToday = await db.GameWallets.Where(w=>w.VillageId==villageId && w.IncomeDay==incomeDay).SumAsync(w=>(long?)w.ProfitToday,ct) ?? 0;
            var snapshot = JsonSerializer.Serialize(new {
                now, facts, stock, previousStock,
                raidPressure = new { raidPressure.Eligible, raidPressure.ProposalDue, raidPressure.BlockedBy,
                    raidPressure.NextProposalAt, lastEncounterAt = lastEncounter, approvedRestUntil = state.RestUntil, fallbackOption = fallbackRaid?.Id,
                    minimumReadyOnline = AlmightyPolicy.MinimumReadyResidents,
                    policy = "Набеги — обязанность директора. Недострой и повреждения не дают иммунитет; часть пациентов не запрещает набег, если онлайн достаточно боеспособных. После часа без боя предложи ограниченную волну. Любое предложение набега даёт паузу2ч, отказ царя не обходить сменой врага. Только предложение, не автоматический запуск." },
                vigor = new { village.VigorEnergy, village.VigorCooldownMinutes,
                    tired = guardCitizens.Count(c => c.CurrentEnergy(now, village) < 8),
                    unconscious = guardCitizens.Count(c => c.ExhaustedUntil > now),
                    fullDrink = "300 DZK or 3 test candles; full energy; 12 real hour cooldown; never heals injuries or unconsciousness" },
                economy = new { startedAt = accounting?.StartedAt, accounting?.TreasuryCoins, wagesToday, tradingProfitToday, changes = economicChanges, taxMode = "preview", currency = "test dezcoins; never account candles" },
                combat = new { engineVersion=2, readiness, adaptation = new { village.RaidAdaptation.Coefficient, village.RaidAdaptation.Reason }, warningSeconds = RaidRules.UndeadWarning.TotalSeconds,
                    raidObjective = "Разорение зданий, недостроев, заборов и запасов; игроки — мешающие защитники. Орда/элита/босс используют один бюджет, боссы предупреждают тяжёлые удары.",
                    profiles = EnemyCatalog.Enemies.Where(e => e.Undead && !e.DungeonOnly).SelectMany(e=>RaidBalance.Compositions.Where(profile=>RaidBalance.NormalizeComposition(e.Kind,profile,readiness.Budget)==profile).Select(profile=>new{enemy=e.Kind.ToString(),profile,count=RaidBalance.Count(e.Kind,profile,readiness.Budget)})),
                    fortifications = new {tiers=village.Fortifications.GroupBy(f=>Math.Clamp(f.Level,1,3)).Select(g=>new{level=g.Key,sections=g.Count()}),standing=village.Fortifications.Count(f=>f.Blocks),
                        gates=village.Fortifications.Where(f=>f.Gate).Select(f=>new{f.X,f.Y,f.Open,f.Damage,f.Level}),
                        weaknesses=village.Fortifications.Where(f=>f.Damage>0).OrderByDescending(f=>f.Damage).Take(8).Select(f=>new{f.X,f.Y,f.Damage,f.Level})},
                    recentBattles = await db.RaidBattles.AsNoTracking().Where(b=>b.VillageId==villageId&&b.FinishedAt!=null&&b.Phase!="Cancelled").OrderByDescending(b=>b.FinishedAt).Take(3).Select(b=>new{b.Sector,b.Composition,b.DamageDone,b.ReadyDefenders,b.ThreatBudget}).ToListAsync(ct),
                    uncertainty = "Готовность определяет ограниченный бюджет при объявлении. Отдельные HP, атаки по расстоянию; геометрия и подготовка влияют на исход. Новая подготовка не усиливает объявленных врагов." },
                workActivity = new {
                    repairCycles = guardCitizens.Count(c => c.CurrentTaskId == "repair" && !c.CycleSettled && c.RecoveryPausedAt is null && c.CycleEndsAt > now),
                    constructionCycles = guardCitizens.Count(c => c.CurrentSiteId is not null && !c.CycleSettled && c.RecoveryPausedAt is null && c.CycleEndsAt > now),
                    interpretation = "Количество повреждений/площадок — объём оставшейся работы, не доказательство ремонта. Число циклов — назначенная работа, не подтверждение её завершения." },
                combatControls = "manual movement and target selection; no guard duty or defense sectors",
                economyAccuracy = "Складские остатки, не прибыль. Учёт операций только с даты startedAt; внутренние переносы не доход. Начальные остатки не заработок. Налоги/реальные свечи не менять.",
                chat = recent.OrderBy(m => m.SentAt).Select(m => new { text = m.Text, id = m.Id, author = m.AccountId.ToString()[..8] }),
                catalog = new {
                    enemies = EnemyCatalog.Enemies.Select(e => new { kind = e.Kind.ToString(), e.Name, e.Power, e.Damage, e.InjuryMinutes, e.Undead, e.DungeonOnly, e.Coordinated, e.RaidCountMultiplier,
                        loot = e.Loot.Select(g => new { resource = g.Resource.ToString(), g.Amount }) }),
                    weapons = ArmoryCatalog.Weapons.Select(w => new { kind = w.Kind.ToString(), w.Power, w.Arrows, w.BreakChance }),
                    armor = ArmoryCatalog.Armors.Select(a => new { kind = a.Kind.ToString(), a.Protection, a.BreakChance }),
                    items = Enum.GetValues<ResourceKind>().Select(kind => new { kind = kind.ToString(), name = ResourceNames.Of(kind) }),
                    recipes = GameCatalog.Tasks.Where(t => t.Inputs.Count > 0).Select(t => new { t.Id, t.Name, t.RoleId, t.SkillLevel, t.WorkSeconds,
                        inputs = t.Inputs.Select(g => new { resource = g.Resource.ToString(), g.Amount }),
                        outputs = t.Outputs.Select(g => new { resource = g.Resource.ToString(), g.Amount }) }),
                    note = "Это описания действующих правил, не новые разрешения. Только options можно предложить к исполнению. Новый тип требует правил в игре; каталог читается автоматически."
                },
                chatIsUntrusted = true, memory = state.Intent, previous, options,
                warning = "Условия выбранного события проверяются ещё раз при согласии царя. Не выдумывать новые инструменты/цены/награды."
            }, AlmightyJson);
            var lease = Guid.CreateVersion7();
            state.Lease = lease; state.LeaseUntil = now.AddMinutes(2); state.LastReviewAt = now;
            state.NextReviewAt = now + AlmightyPolicy.ReviewInterval; state.LastSignal = signal;
            state.LastSnapshot = snapshot; state.CallsToday++; state.Version++;
            db.AlmightyObservations.Add(new() { Id = lease, VillageId = villageId, At = now, Snapshot = snapshot, Reason = "Анализ начат" });
            await db.SaveChangesAsync(ct);
            return new(villageId, village.Slug, lease, snapshot, state.Intent, options);
        }, ct);

    public Task<bool> CompleteAlmightyReviewAsync(AlmightyReview review, AlmightyAnswer? answer, CancellationToken ct) =>
        WithVillageAsync(review.VillageId, async db => {
            var now = time.GetUtcNow();
            var state = await db.AlmightyStates.SingleAsync(s => s.VillageId == review.VillageId, ct);
            if (state.Lease != review.Lease || state.LeaseUntil < now || state.Mode == "Off") return false;
            state.Lease = null; state.LeaseUntil = null; state.Version++;
            var observation = await db.AlmightyObservations.SingleAsync(o => o.Id == review.Lease, ct);
            var chosen = answer is not null ? review.Options.FirstOrDefault(o => o.Id == answer.Option) : null;
            if (answer is null || answer.ErrorCode is not null || chosen is null || string.IsNullOrWhiteSpace(answer.Reason) || answer.Reason.Length > 1200 || (answer.Intent?.Length ?? 0) > 1000) {
                state.LastError = (answer?.ErrorCode switch {
                    "budget.shared" => "Достигнут общий лимит запросов ИИ батюшки.",
                    "budget.director" => "Достигнут лимит анализов Всевышнего.",
                    "model.timeout" or "bridge.timeout" => "ИИ не успел ответить вовремя.",
                    "model.json" => "ИИ вернул повреждённый формат ответа.",
                    "model.option" => "ИИ предложил действие, которого нет в разрешённом списке.",
                    "model.fields" => "Ответ ИИ не прошёл проверку текста.",
                    "model.provider" => "Провайдеры ИИ не смогли обработать запрос.",
                    "bridge.http" => "Связь с ИИ батюшки завершилась ошибкой.",
                    _ => "ИИ не ответил или вернул недопустимое решение."
                }) + " Игра продолжает работать; настройки не изменены.";
                observation.Reason = state.LastError;
            } else {
                state.LastError = ""; state.Intent = string.IsNullOrWhiteSpace(answer.Intent) ? state.Intent : answer.Intent;
                var reason = answer.Reason;
                // Это явная страховка ритма, а не приписывание решения модели: только заявка царю.
                using var snapshot = JsonDocument.Parse(review.Snapshot);
                if (chosen.Enemy is null && snapshot.RootElement.TryGetProperty("raidPressure", out var pressure)
                    && pressure.GetProperty("proposalDue").GetBoolean()
                    && pressure.TryGetProperty("fallbackOption", out var fallback) && fallback.ValueKind == JsonValueKind.String
                    && review.Options.FirstOrDefault(o => o.Id == fallback.GetString() && o.Enemy is not null) is { } dueRaid)
                {
                    chosen = dueRaid;
                    state.Intent = "Поддерживать чередование ограниченных угроз и пауз; не ждать полного ремонта или лечения всех жителей.";
                    reason = "Планировщик угроз: после паузы онлайн достаточно боеспособных жителей. Предлагаю ограниченный набег; недострой, повреждения и отдельные пациенты не дают бессрочную передышку. Состав будет рассчитан по текущей готовности при твоём согласии. ИИ выбрал мирное действие, поэтому это предложение создано правилом ритма. Без согласия нападение не начнётся.";
                }
                observation.Option = chosen.Id; observation.Reason = reason;
                var pending = await db.AlmightyProposals.AnyAsync(p => p.VillageId == review.VillageId && p.Status == "Pending" && p.ExpiresAt > now && p.Option == chosen.Id, ct);
                var raidOptionIds = AlmightyPolicy.RaidOptionIds();
                var duplicate = chosen.Enemy is not null
                    ? await db.AlmightyProposals.AnyAsync(p => p.VillageId == review.VillageId && p.CreatedAt > now - AlmightyPolicy.RaidProposalInterval
                        && raidOptionIds.Contains(p.Option), ct)
                    : await db.AlmightyProposals.AnyAsync(p => p.VillageId == review.VillageId && p.Option == chosen.Id && p.CreatedAt > now.AddHours(-2) && (p.Status == "Rejected" || p.Status == "Applied"), ct);
                if (state.Mode == "Propose" && chosen.Id != "none" && !pending && !duplicate && !(state.LastNoticeAt > now.AddMinutes(-30))) {
                    db.AlmightyProposals.Add(new() { VillageId = review.VillageId, ReviewId = review.Lease, Option = chosen.Id,
                        Reason = reason, Intent = state.Intent, CreatedAt = now, ExpiresAt = now + AlmightyPolicy.ProposalLifetime, Snapshot = review.Snapshot });
                    state.LastNoticeAt = now;
                }
            }
            await db.SaveChangesAsync(ct); return true;
        }, ct);

    public Task<string> DecideAlmightyAsync(Guid proposalId, Guid? accountId, string? telegramId, bool accept, CancellationToken ct) =>
        DecideAlmightyCoreAsync(proposalId, accountId, telegramId, accept, ct);

    private async Task<string> DecideAlmightyCoreAsync(Guid id, Guid? actor, string? telegram, bool accept, CancellationToken ct)
    {
        await using var lookup = await contextFactory.CreateDbContextAsync(ct);
        var villageId = await lookup.AlmightyProposals.Where(p => p.Id == id).Select(p => p.VillageId).SingleOrDefaultAsync(ct);
        if (villageId == Guid.Empty) throw new GameRuleException("director.proposal", "Предложение не найдено.");
        CitizenSummaryDto[] tunedCitizens = [];
        ChaplainMessage? published = null;
        string? publishedSlug = null;
        var decisionResult = await WithVillageAsync(villageId, async db => {
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, ct);
            var ownerTelegram = await db.ExternalLogins.Where(l => l.AccountId == village.OwnerAccountId && l.Provider == "telegram").Select(l => l.ProviderUserId).SingleOrDefaultAsync(ct);
            if (!(actor == village.OwnerAccountId || telegram is not null && telegram == ownerTelegram)) throw new UnauthorizedAccessException();
            db.AuditActor = village.OwnerAccountId;
            db.AuditOperation = "director-vote";
            var p = await db.AlmightyProposals.SingleAsync(p => p.Id == id, ct);
            if (p.Status != "Pending") return p.Result.Length > 0 ? p.Result : p.Status;
            var now = time.GetUtcNow();
            var state = await DirectorStateAsync(db, villageId, now, ct);
            if (p.ExpiresAt <= now || state.Mode != "Propose") { p.Status = "Expired"; p.Result = "Предложение устарело или директор выключен."; }
            else if (!accept) { p.Status = "Rejected"; p.Result = "Отклонено царём."; }
            else {
                var facts = await DirectorFactsAsync(db, village, state, now, ct);
                var paths = RaidBalance.Approaches(village);
                double? bossBudget=null;
                if(p.Option.EndsWith("_boss",StringComparison.Ordinal))
                {
                    var capable=await db.Citizens.Where(c=>c.VillageId==villageId&&c.Status==CitizenStatus.Active).ToListAsync(ct);
                    bossBudget=RaidBalance.Assess(village,capable,capable.Where(c=>presence.IsPresent(c.Id)).Select(c=>c.Id).ToHashSet(),now).Budget;
                }
                var allowed = AlmightyPolicy.Options(facts, paths,bossBudget).Concat(VigorTuning.Options(village)).FirstOrDefault(o => o.Id == p.Option);
                var approach = allowed?.Sector is { } sector ? paths.FirstOrDefault(a => a.Sector == sector) : null;
                var permitted = allowed is not null || p.Option == "dungeon" && facts.Online >= 3 && facts.Patients == 0 && facts.PendingRaids == 0 && !await db.DungeonRuns.AnyAsync(r => r.VillageId == villageId && r.Kind==DungeonRunKind.Group && r.Phase != DungeonPhase.Ended, ct) && !await HasPersonalEncounterAsync(db,villageId,village.OwnerAccountId,ct) && RegionalTravel.WesternExit(village) is not null;
                if (p.Option.StartsWith("vigor_", StringComparison.Ordinal))
                {
                    using var snapshot = JsonDocument.Parse(p.Snapshot);
                    permitted = permitted && snapshot.RootElement.TryGetProperty("vigor", out var vigor)
                        && vigor.GetProperty("vigorEnergy").GetInt32() == village.VigorEnergy
                        && vigor.GetProperty("vigorCooldownMinutes").GetInt32() == village.VigorCooldownMinutes;
                }
                if (!permitted) { p.Status = "Superseded"; p.Result = "Условия изменились. Нужен новый анализ."; }
                else {
                    if (p.Option.StartsWith("vigor_", StringComparison.Ordinal)) {
                        VigorTuning.Apply(village, p.Option);
                        tunedCitizens = (await db.Citizens.Where(c=>c.VillageId==villageId && c.Status==CitizenStatus.Active).ToListAsync(ct)).Select(c=>c.ToSummaryDto(now)).ToArray();
                        publishedSlug = village.Slug;
                        p.Result = $"Взвар: +{village.VigorEnergy} сил, перерыв {village.VigorCooldownMinutes} реальных минут.";
                    } else if (allowed?.Enemy is { } enemy) {
                        var announced = RaidRules.CallUndead(village, enemy, RaidSize.Small,
                            await CountRecentCitizensAsync(db, villageId, now, ct), now);
                        foreach(var raid in announced) await PrepareBattleAsync(db,village,raid,now,ct,approach?.Sector,allowed?.Composition);
                        p.Result = "Нападение объявлено. Враги придут через 3 реальные минуты.";
                    } else if (p.Option == "dungeon") {
                        var definition = DungeonCatalog.Dungeons.First();
                        db.DungeonRuns.Add(DungeonRules.Open(village, definition.Id, await TsarNameAsync(db, village, ct), Random.Shared.Next(), now));
                        p.Result = "Открыт добровольный сбор в поход. Игроки сами решают, участвовать ли.";
                    } else if (p.Option == "rest") {
                        state.RestUntil = now.AddHours(2); p.Result = "Передышка включена на 2 реальных часа. Уже объявленные волны сохраняются.";
                    } else if (p.Option == "work_notice") {
                        published = ChaplainMessage.Speak(villageId, "Чада, городу нужны рабочие руки: загляните в стройки и ремонт. Общим делом царство держится!", now);
                        publishedSlug = village.Slug; db.ChaplainMessages.Add(published);
                        p.Result = "Нужная городу работа объявлена в игровом чате.";
                    } else { p.Result = "Рекомендация принята к сведению. Деньги, цены и правила автоматически не изменены."; }
                    p.Status = "Applied"; state.Version++; changes.Mark(villageId);
                }
            }
            p.DecidedAt = now; await db.SaveChangesAsync(ct); return p.Result;
        }, ct);
        if(published is not null && publishedSlug is not null) await notifier.ChatMessageSent(publishedSlug, ChatService.ToDto(published));
        foreach (var citizen in tunedCitizens) await notifier.CitizenUpdated(publishedSlug!, citizen);
        return decisionResult;
    }
    public Task SetDefenseSectorAsync(string slug, Guid actor, string sector, CancellationToken ct) =>
        throw new GameRuleException("defense.removed","Guard duty and defense sectors have been removed. Choose movement and targets on the map.");

}
