using Desertod.Domain.Skills;
using Desertod.Domain.Economy;
using Desertod.Domain.Farming;
using System.Text;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Desertod.Api.Game;

public sealed class NotFoundException(string code, string message, IReadOnlyDictionary<string, object>? arguments = null)
    : Exception(message)
{
    public string Code { get; } = code;

    public IReadOnlyDictionary<string, object> Arguments { get; } = arguments ?? new Dictionary<string, object>();
}

/// <summary>A citizen whose cycle has ended but is not banked yet.</summary>
public sealed record DueCitizen(Guid VillageId, Guid CitizenId);

public sealed partial class GameService(
    IDbContextFactory<DesertodDbContext> contextFactory,
    TimeProvider time,
    VillageNotifier notifier,
    VillageStateChanges changes,
    CitizenPresence presence,
    IHospitalBridge hospitalBridge,
    ILogger<GameService> logger,
    IConfiguration configuration)
{
    private const int MaximumAttempts = 5;

    private static readonly TimeSpan SlowLockThreshold = TimeSpan.FromMilliseconds(100);
    private const string SuperviseTaskId = "supervise";
    private const string BuildTaskId = "build";

    /// <summary>Citizens settled under one hold of the village lock; each takes several queries.</summary>
    // Each cycle already has its own transaction. Yield between residents so combat is not queued behind ten transactions.
    private const int SettlementChunkSize = 1;

    /// <summary>Taken by <c>GET /api/villages/public</c>, so no village may use it.</summary>
    private const string PublicListSlug = "public";

    private readonly VillageLocks locks = new();

    /// <param name="IsDue">The cycle has ended and is not banked yet.</param>
    private sealed record CitizenKey(Guid Id, Guid VillageId, bool IsDue);

    /// <param name="CompletedInOrder">Finished project ids, oldest first.</param>
    private sealed record VillageContext(WorkContext Work, IReadOnlyList<string> CompletedInOrder, IReadOnlyList<Guid> ActiveCitizenIds);

    public async Task<IReadOnlyList<VillageListItemDto>> ListVillagesAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var citizenVillageIds = db.Citizens.Where(c => c.AccountId == accountId).Select(c => c.VillageId);

        return await db.Villages
            .Where(v => v.OwnerAccountId == accountId || citizenVillageIds.Contains(v.Id))
            .OrderBy(v => v.Name)
            .Select(v => new VillageListItemDto(v.Slug, v.Name, v.OwnerAccountId == accountId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Public villages for the home page, the busiest first.</summary>
    public async Task<IReadOnlyList<PublicVillageDto>> ListPublicVillagesAsync(CancellationToken cancellationToken)
    {
        const int limit = 50;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Villages
            .Where(v => !v.IsPrivate)
            .Select(v => new
            {
                Village = v,
                Citizens = db.Citizens.Count(c => c.VillageId == v.Id && c.Status == CitizenStatus.Active),
            })
            .OrderByDescending(x => x.Citizens)
            .ThenBy(x => x.Village.Name)
            .Take(limit)
            .Select(x => new PublicVillageDto(x.Village.Slug, x.Village.Name, x.Village.Description, x.Citizens))
            .ToListAsync(cancellationToken);
    }

    public async Task<VillageStateDto> CreateVillageAsync(Guid accountId, CreateVillageRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var village = Village.Create("pending", request.Name ?? "", request.Description, request.IsPrivate, accountId, now);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var baseSlug = Slugify(village.Name);
        var slug = baseSlug;
        for (var suffix = 2; slug == PublicListSlug || await db.Villages.AnyAsync(v => v.Slug == slug, cancellationToken); suffix++)
        {
            slug = $"{baseSlug}-{suffix}";
        }

        village.Slug = slug;
        KingdomRules.FoundCastle(village,now);
        db.Villages.Add(village);
        await db.SaveChangesAsync(cancellationToken);
        logger.VillageCreated(accountId, slug);

        return await BuildStateAsync(db, village, now, cancellationToken);
    }

    public async Task<VillageDto> GetVillageAsync(string slug, Guid accountId, CancellationToken cancellationToken)
        => await WithVillageAsync(await FindVillageIdAsync(slug,cancellationToken), async db =>
    {
        var village = await LoadVillageAsync(db, slug, cancellationToken);
        if (village.EnsureCamp(time.GetUtcNow()))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var citizens = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        var isOwner = village.OwnerAccountId == accountId;
        return new VillageDto(
            await BuildStateAsync(db, village, now, cancellationToken),
            isOwner,
            isOwner ? village.InviteCode : null,
            citizens.Select(c => c.ToSummaryDto(now)).ToList(),
            citizens.Where(c => !presence.IsPresent(c.Id)).Select(c => c.Id).ToList());
    }, cancellationToken);

    /// <summary>The player's village page connected to the hub; a citizen of this village is present while it stays open.</summary>
    public async Task ConnectAsync(string slug, Guid accountId, string connectionId, CancellationToken cancellationToken)
    {
        if (await FindCitizenAsync(slug, accountId, cancellationToken) is { } key && presence.Connect(key.Id, slug, connectionId))
        {
            await notifier.CitizenBack(slug, key.Id);
        }
    }

    public void Disconnect(string connectionId) => presence.Disconnect(connectionId);

    /// <summary>The citizen did something, so they are present.</summary>
    private async Task SeeAsync(string slug, Guid citizenId)
    {
        if (presence.Touch(citizenId, slug))
        {
            await notifier.CitizenBack(slug, citizenId);
        }
    }

    public async Task<InviteDto> NewInviteCodeAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            if (!village.IsPrivate)
            {
                throw new GameRuleException("village.notPrivate", "Anyone can join this village; it needs no invite code.");
            }

            var code = village.NewInviteCode();
            await db.SaveChangesAsync(cancellationToken);
            logger.TsarActed(slug, "newInviteCode", "");
            return new InviteDto(code);
        }, cancellationToken);

    public async Task<CitizenDto?> GetCitizenAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await FindCitizenAsync(slug, accountId, cancellationToken);
        if (key is null)
        {
            return null;
        }

        await WithVillageAsync(key.VillageId, async context => { await SyncHospitalAsync(context, key.VillageId, cancellationToken); return true; }, cancellationToken);
        await SeeAsync(slug, key.Id);
        if (key.IsDue)
        {
            await SettleVillageAsync(key.VillageId, [key.Id], cancellationToken);
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
        var village = await db.Villages.AsNoTracking().SingleAsync(v => v.Id == key.VillageId, cancellationToken);
        return citizen.ToDto(village, time.GetUtcNow());
    }

    public async Task<CitizenDto> JoinVillageAsync(
        string slug,
        Guid accountId,
        JoinVillageRequest request,
        CancellationToken cancellationToken)
    {
        var villageId = await FindVillageIdAsync(slug, cancellationToken);
        CitizenDto created;
        using (await AcquireLockAsync(villageId, cancellationToken))
        {
            created = await AddCitizenAsync(slug, accountId, request, cancellationToken);
        }

        await RefreshQuestsAsync(villageId, slug, cancellationToken);
        return created;
    }

    private async Task<CitizenDto> AddCitizenAsync(
        string slug,
        Guid accountId,
        JoinVillageRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var village = await LoadVillageAsync(db, slug, cancellationToken);
        if (village.OwnerAccountId == accountId)
        {
            throw new GameRuleException("citizen.tsar", "The Tsar rules the village and cannot live in it as a citizen.");
        }

        if (await db.Citizens.AnyAsync(c => c.VillageId == village.Id && c.AccountId == accountId, cancellationToken))
        {
            throw new GameRuleException("citizen.alreadyLives", "You already live in this village.");
        }

        if (!village.Admits(request.InviteCode))
        {
            throw string.IsNullOrWhiteSpace(request.InviteCode)
                ? new GameRuleException("village.inviteRequired", "This village is private. Ask the Tsar for the invite code.")
                : new GameRuleException("village.inviteInvalid", "This invite code is wrong or no longer valid.");
        }

        var character = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => a.Character)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new GameRuleException("character.missing", "Create your character first.");

        var now = time.GetUtcNow();
        var citizen = CharacterCreation.Create(village.Id, accountId, character, now);
        db.Citizens.Add(citizen);
        var housed = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .ToListAsync(cancellationToken);
        ShelterRules.Assign(village, [.. housed, citizen], now);
        await db.SaveChangesAsync(cancellationToken);
        logger.CitizenJoined(accountId, village.Slug, citizen.Id);
        presence.Touch(citizen.Id, village.Slug);

        await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
        return citizen.ToDto(village, now);
    }

    /// <summary>The citizen leaves the village for good; see <see cref="Departure.Leave"/>. Their chat lines go with them.</summary>
    public async Task LeaveVillageAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        ProtectPrimaryWorld(slug);
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        if (key.IsDue)
        {
            await SettleVillageAsync(key.VillageId, [key.Id], cancellationToken);
        }

        await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var run = citizen.DungeonRunId is { } runId
                ? await db.DungeonRuns.SingleOrDefaultAsync(r => r.Id == runId, cancellationToken)
                : null;
            var now = time.GetUtcNow();
            village.EnsureCamp(now);
            Departure.Leave(citizen, village, run, now);
            await FinishWorkOrderAsync(db, village, citizen, false, true, now, cancellationToken);

            await db.ChatMessages.Where(m => m.CitizenId == citizen.Id).ExecuteDeleteAsync(cancellationToken);
            db.Citizens.Remove(citizen);
            var housed = await db.Citizens
                .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active && c.Id != citizen.Id)
                .ToListAsync(cancellationToken);
            ShelterRules.Assign(village, housed, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenLeft(accountId, slug, citizen.Id);
            presence.Forget(citizen.Id);

            await notifier.CitizenLeft(slug, citizen.Id);
            changes.Mark(village.Id);
            return true;
        }, cancellationToken);

        await RefreshQuestsAsync(key.VillageId, slug, cancellationToken);
    }

    /// <summary>
    /// The Tsar deletes the village with its citizens, quests, projects, runs and chat. Not while heroes are away in a
    /// dungeon: their fight runs in memory and comes back to the village.
    /// </summary>
    public async Task DeleteVillageAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        ProtectPrimaryWorld(slug);
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            if (await db.DungeonRuns.AnyAsync(
                    r => r.VillageId == village.Id && (r.Phase == DungeonPhase.Marching || r.Phase == DungeonPhase.Running),
                    cancellationToken))
            {
                throw new GameRuleException("village.heroesAway", "Wait until the heroes are back from the dungeon.");
            }

            await db.Villages.Where(v => v.Id == village.Id).ExecuteDeleteAsync(cancellationToken);
            logger.VillageDeleted(accountId, slug);

            await notifier.VillageDeleted(slug);
            return true;
        }, cancellationToken);

    }

    private void ProtectPrimaryWorld(string slug)
    {
        if (configuration["Hosting:PrimaryVillage"] is {Length: > 0} primary && string.Equals(primary, slug, StringComparison.OrdinalIgnoreCase))
            throw new GameRuleException("village.protectedWorld", "This kingdom and its characters cannot be deleted. Sign out to leave the game.");
    }

    /// <summary>A newcomer can leave the tribe short of beds.</summary>
    private Task RefreshQuestsAsync(Guid villageId, string slug, CancellationToken cancellationToken) =>
        WithVillageAsync(villageId, async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            var now = time.GetUtcNow();
            var quests = await UpdateQuestsAsync(db, village, null, null, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var quest in quests)
            {
                logger.QuestCompleted(village.Slug, quest.Kind.ToString());
                await notifier.QuestCompleted(village.Slug, quest);
            }

            changes.Mark(village.Id);
            return true;
        }, cancellationToken);

    public async Task<CitizenDto> StartTaskAsync(string slug, Guid accountId, StartTaskRequest request, CancellationToken cancellationToken)
    {
        if (request.Ownership == WorkOwnership.PublicContract && request.WorkOrderId is null
            || request.WorkOrderId is not null && request.Ownership is not (null or WorkOwnership.PublicContract)
            || request.WorkOrderId is not null && (request.RequestId is null || request.RequestId == Guid.Empty)
            || request.RequestId == Guid.Empty)
            throw new GameRuleException("workOrder.ownership", "Choose the work order before starting contracted work.");
        var task = GameCatalog.FindTask(request.TaskId)
                   ?? throw new GameRuleException("task.unknown", $"Unknown task \"{request.TaskId}\".",
                       new Dictionary<string, object> { ["taskId"] = request.TaskId });
        if(task.Effect==TaskEffect.Guard)throw new GameRuleException("task.retired","Guard duty has been removed. Choose movement and enemies on the map.");
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await StartAsync(
            key,
            "startTask",
            $"{request.TaskId} {request.Spot}",
            (citizen, context, now) => CycleEngine.Start(citizen, task, context, now, request.Spot, request.WorkplacePlotId,
                request.WorkOrderId is null ? request.Ownership : WorkOwnership.PublicContract,request.FarmTarget),
            cancellationToken,
            request.WorkOrderId, request.RequestId,
            StartTaskFingerprint(request));
    }

    private static string StartTaskFingerprint(StartTaskRequest request)
    {
        object body=request.FarmTarget is null
            ? new {request.TaskId,request.Spot,request.WorkplacePlotId,request.Ownership,request.WorkOrderId,request.RequestId}
            : request;
        return "start:"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body)));
    }

    /// <summary>The citizen researches the technology the village voted for, at its building.</summary>
    public async Task<CitizenDto> StartResearchAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await StartAsync(key, "startResearch", "", CycleEngine.StartResearch, cancellationToken);
    }

    /// <summary>A citizen votes in the open poll; the poll closes once more than half of the active citizens voted.</summary>
    public async Task<VillageStateDto> VoteAsync(string slug, Guid accountId, string technologyId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            TechnologyRules.CastVote(village, citizen, technologyId, await CountElectorateAsync(db, village, cancellationToken), now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, "vote", technologyId);

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>The Tsar or a Governor puts some available technologies to the vote of the citizens.</summary>
    public async Task<VillageStateDto> OpenPollAsync(string slug, Guid accountId, IReadOnlyList<string> technologyIds, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            var ruler = await RequireRulerAsync(db, village, accountId, cancellationToken);

            var now = time.GetUtcNow();
            TechnologyRules.OpenPoll(village, technologyIds, await CountActiveCitizensAsync(db, village, cancellationToken), now);
            await db.SaveChangesAsync(cancellationToken);
            logger.RulerActed(slug, ruler, "openPoll", string.Join(", ", technologyIds));

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);

    public Task<VillageStateDto> CancelPollAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        PlanAsync(slug, accountId, "cancelPoll", "", TechnologyRules.CancelPoll, cancellationToken);

    private static Task<int> CountActiveCitizensAsync(DesertodDbContext db, Village village, CancellationToken cancellationToken) =>
        db.Citizens.CountAsync(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active, cancellationToken);

    /// <summary>Active citizens who are present; away citizens do not hold a vote open.</summary>
    private async Task<int> CountElectorateAsync(DesertodDbContext db, Village village, CancellationToken cancellationToken)
    {
        var ids = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
        return ids.Count(presence.IsPresent);
    }

    public async Task<CitizenDto> StartJobAsync(string slug, Guid accountId, StartJobRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await StartAsync(
            key,
            "startJob",
            $"{request.JobId} at {request.PlotId} stage {request.Stage}",
            (citizen, context, now) => CycleEngine.StartJob(citizen, request.PlotId, request.Stage, request.JobId, context, now),
            cancellationToken);
    }

    /// <summary>Settles the finished cycle, then starts the next task or job in the same transaction.</summary>
    private Task<CitizenDto> StartAsync(
        CitizenKey key,
        string action,
        string detail,
        Action<Citizen, WorkContext, DateTimeOffset> start,
        CancellationToken cancellationToken,
        Guid? workOrderId = null, Guid? requestId = null, string? fingerprint = null) =>
        WithVillageAsync(key.VillageId, async db =>
        {
            if (requestId is {} id && await db.EconomyReceipts.SingleOrDefaultAsync(r=>r.VillageId==key.VillageId && r.RequestId==id,cancellationToken) is {} prior)
            {
                var actor=await db.Citizens.Where(c=>c.Id==key.Id).Select(c=>c.AccountId).SingleAsync(cancellationToken);
                if (prior.AccountId!=actor || prior.Fingerprint!=fingerprint)
                    throw new GameRuleException("economy.reusedRequest","This request was already used for another operation.");
                var accepted=await db.Citizens.SingleAsync(c=>c.Id==key.Id,cancellationToken);
                var world=await db.Villages.SingleAsync(v=>v.Id==key.VillageId,cancellationToken);
                return accepted.ToDto(world,time.GetUtcNow());
            }
            await SyncHospitalAsync(db, key.VillageId, cancellationToken, true);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            HospitalRules.RequireReady(citizen, time.GetUtcNow());
            SafeRest.RequireActive(citizen.RestState);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            village.EnsureCamp(now);
            var context = (await LoadContextAsync(db, village, citizen.Id, now, cancellationToken)).Work;
            var raids = await FightDueRaidsAsync(db, village, context.CompletedProjects, now, cancellationToken);

            var report = await SettleWorkAsync(db, citizen, context, now, cancellationToken);
            await AfterSettleAsync(db, village, report, now, cancellationToken);
            var completion = await CompleteProjectIfDoneAsync(db, context, now, cancellationToken);
            if (completion is not null)
            {
                context = (await LoadContextAsync(db, village, citizen.Id, now, cancellationToken)).Work;
            }

            var quests = await UpdateQuestsAsync(db, village, citizen, report, now, cancellationToken);
            start(citizen, context, now);
            await AcceptWorkOrderAsync(db, village, citizen, workOrderId, now, cancellationToken);
            if (requestId is {} commandId)
                db.EconomyReceipts.Add(new EconomyReceipt{VillageId=village.Id,RequestId=commandId,AccountId=citizen.AccountId,Fingerprint=fingerprint!,At=now});
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, action, detail);

            await BroadcastRaidsAsync(village, raids, now);
            await BroadcastEventsAsync(village, citizen, report, completion, quests, now);
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);

    /// <summary>A Synoptic who has just slept announces the weather of the next day or night.</summary>
    public async Task<VillageStateDto> ForecastAsync(string slug, Guid accountId, bool rain, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            await SyncHospitalAsync(db, key.VillageId, cancellationToken);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            HospitalRules.RequireReady(citizen, time.GetUtcNow());
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            WeatherRules.Forecast(village, citizen, rain, Random.Shared, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, "forecast", rain ? "rain" : "dry");

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>The Tsar places directly; a Governor submits a construction application for the Tsar.</summary>
    public async Task<VillageStateDto> PlaceBlueprintAsync(
        string slug, Guid accountId, PlaceBlueprintRequest request, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            var ruler = await RequireRulerAsync(db, village, accountId, cancellationToken);
            db.AuditActor = accountId;
            db.AuditOperation = "placeBlueprint:" + request.Kind;
            var state = await ActAsync(db, village, (city, now) =>
            {
                if (city.OwnerAccountId == accountId)
                    ConstructionRules.Place(city, request.Kind, request.X, request.Y, now).PlacedByAccountId=accountId;
                else
                    BuildingOperations.RequestBuild(city, accountId, request.Kind, request.X, request.Y, now);
            }, cancellationToken);
            logger.RulerActed(slug, ruler, "placeBlueprint", $"{request.Kind} at {request.X},{request.Y}");
            return state;
        }, cancellationToken);

    /// <summary>The Tsar walks to a spot on the land; every viewer sees them go.</summary>
    public Task<VillageStateDto> TsarGoToAsync(string slug, Guid accountId, GoToRequest request, CancellationToken cancellationToken) =>
        RuleAsync(slug, accountId, "goTo", request.Spot.ToString(), (village, now) => village.TsarGoTo(request.Spot, now), cancellationToken);

    public async Task<VillageStateDto> CancelBlueprintAsync(string slug, Guid accountId, string plotId, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,cancellationToken),async db=>
        {
            var village=await LoadVillageAsync(db,slug,cancellationToken);
            var site=ConstructionRules.FindSite(village,plotId);
            if(site.OwnerAccountId is {} owner && owner!=accountId)throw new UnauthorizedAccessException();
            if(site.OwnerAccountId is null)await RequireRulerAsync(db,village,accountId,cancellationToken);
            db.AuditActor=accountId;db.AuditOperation="cancelBlueprint:"+plotId;
            ConstructionRules.Cancel(village,plotId);
            await db.SaveChangesAsync(cancellationToken);changes.Mark(village.Id);
            return await BuildSavedStateAsync(db,slug,time.GetUtcNow(),cancellationToken);
        },cancellationToken);

    /// <summary>The Tsar writes a subtitle under a citizen's name on the map.</summary>
    public Task<CitizenSummaryDto> SetSubtitleAsync(
        string slug,
        Guid accountId,
        Guid citizenId,
        string? subtitle,
        CancellationToken cancellationToken) =>
        ChangeCitizenAsync(slug, accountId, citizenId, "setSubtitle", citizen => citizen.SetSubtitle(subtitle), cancellationToken);

    /// <summary>The Tsar appoints a citizen Governor, or dismisses them.</summary>
    public Task<CitizenSummaryDto> SetGovernorAsync(
        string slug,
        Guid accountId,
        Guid citizenId,
        bool isGovernor,
        CancellationToken cancellationToken) =>
        ChangeCitizenAsync(
            slug,
            accountId,
            citizenId,
            isGovernor ? "appointGovernor" : "dismissGovernor",
            citizen => {citizen.IsGovernor = isGovernor;if(!isGovernor)citizen.CanManageFarm=false;},
            cancellationToken);

    /// <summary>A change the Tsar makes to one citizen of the village.</summary>
    private async Task<CitizenSummaryDto> ChangeCitizenAsync(
        string slug,
        Guid accountId,
        Guid citizenId,
        string actionName,
        Action<Citizen> change,
        CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.Id == citizenId && c.VillageId == village.Id, cancellationToken)
                          ?? throw new NotFoundException("citizen.notFound", "This citizen does not live in the village.");
            change(citizen);
            db.AuditActor = accountId;
            db.AuditOperation = actionName;
            await db.SaveChangesAsync(cancellationToken);
            logger.TsarActed(slug, actionName, citizenId.ToString());

            var summary = citizen.ToSummaryDto(time.GetUtcNow());
            await notifier.CitizenUpdated(village.Slug, summary);
            return summary;
        }, cancellationToken);

    /// <summary>An action only the Tsar may take.</summary>
    private async Task<VillageStateDto> RuleAsync(
        string slug,
        Guid accountId,
        string actionName,
        string detail,
        Action<Village, DateTimeOffset> action,
        CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            if(actionName=="goTo")await RequireMovementReadyAsync(db,village.Id,accountId,null,cancellationToken);
            db.AuditActor = accountId; db.AuditOperation = (actionName + ":" + detail)[..Math.Min(80, actionName.Length + detail.Length + 1)];
            var state = await ActAsync(db, village, action, cancellationToken);
            logger.TsarActed(slug, actionName, detail);
            return state;
        }, cancellationToken);

    /// <summary>An action the Tsar and the Governors may take: planning buildings and calling votes.</summary>
    private async Task<VillageStateDto> PlanAsync(
        string slug,
        Guid accountId,
        string actionName,
        string detail,
        Action<Village, DateTimeOffset> action,
        CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            var ruler = await RequireRulerAsync(db, village, accountId, cancellationToken);
            db.AuditActor = accountId; db.AuditOperation = (actionName + ":" + detail)[..Math.Min(80, actionName.Length + detail.Length + 1)];
            var state = await ActAsync(db, village, action, cancellationToken);
            logger.RulerActed(slug, ruler, actionName, detail);
            return state;
        }, cancellationToken);

    private async Task<VillageStateDto> ActAsync(
        DesertodDbContext db,
        Village village,
        Action<Village, DateTimeOffset> action,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        village.EnsureCamp(now);
        village.Explore(await LoadTripsAsync(db, village.Id, cancellationToken), now);
        action(village, now);
        await db.SaveChangesAsync(cancellationToken);

        changes.Mark(village.Id);
        return await BuildStateAsync(db, village, now, cancellationToken);
    }

    /// <summary>The Tsar, or an active citizen the Tsar appointed Governor. Returns "Tsar" or the Governor's citizen id, for the log.</summary>
    private static async Task<string> RequireRulerAsync(DesertodDbContext db, Village village, Guid accountId, CancellationToken cancellationToken)
    {
        if (village.OwnerAccountId == accountId)
        {
            return "Tsar";
        }

        var governorId = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.AccountId == accountId && c.IsGovernor && c.Status == CitizenStatus.Active)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return governorId is { } id ? $"Governor {id}" : throw new UnauthorizedAccessException();
    }

    /// <summary>A finished shelter changes who sleeps where.</summary>
    private async Task AfterSettleAsync(
        DesertodDbContext db,
        Village village,
        CycleReport? report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await RecordFarmChangesAsync(db,village,report,cancellationToken);
        await PayUsefulWorkAsync(db, village, report, now, cancellationToken);
        await RescueSweepAsync(db,village,now,cancellationToken);
        if (report?.BuildingCompleted is not { } kind || BuildingCatalog.Get(kind).Shelter == 0)
        {
            return;
        }

        var citizens = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .ToListAsync(cancellationToken);
        ShelterRules.Assign(village, citizens, now);
    }

    /// <summary>The citizen throws away goods that do not fit in storage, to get back to the same job.</summary>
    public async Task<CitizenDto> DropLoadAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            await SyncHospitalAsync(db, key.VillageId, cancellationToken);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            HospitalRules.RequireReady(citizen, time.GetUtcNow());
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            StorageRules.Drop(citizen, village);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "dropLoad", "");

            var now = time.GetUtcNow();
            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    public async Task<CitizenDto> EatAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            await SyncHospitalAsync(db, key.VillageId, cancellationToken);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var now = time.GetUtcNow();
            HospitalRules.RequireReady(citizen, now);
            var village = await db.Villages.SingleAsync(v => v.Id == key.VillageId, cancellationToken);
            if (citizen.Status != CitizenStatus.Active || citizen.DungeonRunId is not null || citizen.ExhaustedUntil > now)
                throw new GameRuleException("inventory.unavailable", "Wait until recovery or the expedition is finished.");
            if (!citizen.IsHungry) throw new GameRuleException("food.notHungry", "You are already fed.");
            if (!FoodRules.Eat(citizen, village, now, allowPublic: false))
                throw new GameRuleException("food.missing", "There is no food in your hands or personal inventory.");
            db.AuditActor = accountId; db.AuditOperation = "eat";
            await db.SaveChangesAsync(cancellationToken);
            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The citizen drinks a vigor drink; see <see cref="VigorDrinks.Drink"/>.</summary>
    public async Task<CitizenDto> DrinkAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            await SyncHospitalAsync(db, key.VillageId, cancellationToken);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            HospitalRules.RequireReady(citizen, time.GetUtcNow());
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            VigorDrinks.Drink(citizen, village, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "drink", "");

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The wounded citizen uses a remedy; see <see cref="CombatRules.Heal"/>.</summary>
    public async Task<CitizenDto> HealAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            var raids = await FightDueRaidsAsync(db, village, null, now, cancellationToken);
            CombatRules.Heal(citizen, village, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "heal", "");

            await BroadcastRaidsAsync(village, raids, now);
            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The Tsar calls the dead up for the viewers: waves of zombies or skeletons attack the camp.</summary>
    public async Task<VillageStateDto> CallUndeadAsync(string slug, Guid accountId, CallUndeadRequest request, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            var now = time.GetUtcNow();
            var announced = RaidRules.CallUndead(village, request.Enemy, request.Size, await CountRecentCitizensAsync(db, village.Id, now, cancellationToken), now);
            foreach(var raid in announced) await PrepareBattleAsync(db,village,raid,now,cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            logger.TsarActed(slug, "callUndead", $"{request.Size} {request.Enemy}");

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);

    public async Task<VillageStateDto> CancelUndeadAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,cancellationToken),async db=>
        {
            var village=await LoadVillageAsync(db,slug,cancellationToken);
            if(village.OwnerAccountId!=accountId)throw new UnauthorizedAccessException();
            var now=time.GetUtcNow();
            db.AuditActor=accountId; db.AuditOperation="cancel-undead";
            var pending=village.Raids.Where(r=>r.IsPending&&r.Source==RaidSource.Undead).ToArray();
            var ids=pending.Select(r=>r.Id).ToArray();
            var people=await db.Citizens.Where(c=>c.VillageId==village.Id).ToListAsync(cancellationToken);
            var battles=await db.RaidBattles.Where(b=>ids.Contains(b.Id)&&b.FinishedAt==null).ToListAsync(cancellationToken);
            foreach(var raid in pending)
            {
                var battle=battles.SingleOrDefault(b=>b.Id==raid.Id);
                if(battle is not null) RaidSimulation.Cancel(village,raid,battle,people,now);
                else {raid.Outcome=RaidOutcome.Cancelled;raid.ResolvedAt=now;}
            }
            RaidRules.UpdateNextRaidAt(village);
            await db.SaveChangesAsync(cancellationToken);changes.Mark(village.Id);
            foreach(var battle in battles) await notifier.BattleUpdated(village.Slug,battle,village.Fortifications);
            foreach(var person in people) await notifier.CitizenUpdated(village.Slug,person.ToSummaryDto(now));
            return await BuildStateAsync(db,village,now,cancellationToken);
        },cancellationToken);

    /// <summary>Villages with a raid due, or whose beasts have not decided yet about tonight.</summary>
    public async Task<IReadOnlyList<Guid>> FindRaidVillagesAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var period = DayCycle.PeriodOf(now);
        var night = DayCycle.IsNight(period);
        var recentSince = now - GameRules.RecentActivityWindow;
        return await db.Villages
            .Where(v => v.NextRaidAt <= now
                        || (night && v.RaidsPlannedPeriod < period
                                  && db.Citizens.Any(c => c.VillageId == v.Id && c.CycleStartedAt >= recentSince)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Plans tonight's raid of a village and fights the raids that struck.</summary>
    public Task RaidVillageAsync(Guid villageId, CancellationToken cancellationToken) =>
        WithVillageAsync(villageId, async db =>
        {
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
            var now = time.GetUtcNow();
            var resting = await db.AlmightyStates.AnyAsync(s => s.VillageId == villageId && s.Mode != "Off" && s.RestUntil > now, cancellationToken);
            var planned = resting || village.DefeatedAt is not null ? null : RaidRules.PlanNight(village, await CountRecentCitizensAsync(db, villageId, now, cancellationToken), Random.Shared, now);
            if(planned is not null)
            {
                try{await PrepareBattleAsync(db,village,planned,now,cancellationToken);}
                catch(GameRuleException e) when(e.Code=="raid.noApproach")
                {
                    village.Raids.Remove(planned);RaidRules.UpdateNextRaidAt(village);planned=null;
                    logger.LogWarning("Night raid skipped: no reachable approach in village {VillageId}.",villageId);
                }
            }
            var raids = await FightDueRaidsAsync(db, village, null, now, cancellationToken);
            var saveStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            await db.SaveChangesAsync(cancellationToken);
            CombatDiagnostics.Stage(villageId,"save",System.Diagnostics.Stopwatch.GetElapsedTime(saveStarted).TotalMilliseconds);
            if (planned is not null)
            {
                logger.RaidPlanned(village.Slug, planned.Count, planned.Enemy.ToString(), planned.StrikesAt);
            }

            await BroadcastRaidsAsync(village, raids, now);
            if(raids.WorldChanged)changes.Mark(villageId);
            if (planned is not null || raids.Reports.Count > 0)
            {
                changes.Mark(villageId);
                await db.RaidBattles.Where(b=>b.VillageId==villageId&&b.FinishedAt<now.AddDays(-14)).ExecuteDeleteAsync(cancellationToken);
            }

            return true;
        }, cancellationToken);

    private static Task<int> CountRecentCitizensAsync(DesertodDbContext db, Guid villageId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recentSince = now - GameRules.RecentActivityWindow;
        return db.Citizens.CountAsync(
            c => c.VillageId == villageId && c.Status == CitizenStatus.Active && c.CycleStartedAt >= recentSince, cancellationToken);
    }

    /// <param name="Citizens">Defenders and wounded, to send their new state.</param>
    private sealed record RaidsFought(IReadOnlyList<RaidReport> Reports, IReadOnlyList<Citizen> Citizens, bool Changed = false, IReadOnlyList<RaidBattle>? Battles = null, IReadOnlyList<Fortification>? Walls = null, bool WorldChanged = false);

    /// <summary>
    /// Fights the raids that struck by now, before anything else changes the citizens: who was on watch at the moment of
    /// the strike is read from their running cycles.
    /// </summary>
    /// <param name="completedProjects">Loaded here when null.</param>
    private async Task<RaidsFought> FightDueRaidsAsync(
        DesertodDbContext db,
        Village village,
        IReadOnlyCollection<string>? completedProjects,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await SyncHospitalAsync(db, village.Id, cancellationToken, actorAccount:village.OwnerAccountId);
        var due = RaidRules.Due(village, now);
        if (due.Count == 0)
        {
            return new RaidsFought([], []);
        }

        completedProjects ??= await db.VillageProjects
            .Where(p => p.VillageId == village.Id && p.Status == ProjectStatus.Completed)
            .Select(p => p.DefinitionId)
            .ToListAsync(cancellationToken);
        // Preparation may complete the palisade after announcement; its real geometry helps
        // without increasing the already frozen attacker budget.
        Fortifications.Ensure(village,completedProjects);
        var citizens = await db.Citizens.Where(c => c.VillageId == village.Id).ToListAsync(cancellationToken);
        var injuredBefore = citizens.Where(c=>CombatRules.IsInjured(c,now)).Select(c=>c.Id).ToHashSet();
        // Only collapse/relocation needs a full world refresh. Per-second movement is
        // already carried by BattleUpdated and must not retransmit the entire village.
        var worldBefore=(village.BridgeOpened,village.BridgeRuinedAt,village.DefeatedAt,
            Ruins:CombatRules.Buildings(village).Count(b=>b.Damage>=100));
        var peopleBefore=citizens.ToDictionary(c=>c.Id,c=>(c.HomePlotId,c.CurrentSiteId,c.CycleSettled,
            Ward:c.HospitalizedAt is not null?c.IdleSpot:null));
        var reports = new List<RaidReport>();
        var changed = false;
        var updated = new List<RaidBattle>();
        var liveIds=due.Where(r=>r.EngineVersion>=2).Select(r=>r.Id).ToArray();
        var battles=await db.RaidBattles.Where(b=>liveIds.Contains(b.Id)).ToDictionaryAsync(b=>b.Id,cancellationToken);
        // Active combat always keeps its turn. Waiting waves retain their original schedule,
        // even after a previous wave pushes their actual start past the next wave's deadline.
        var allowed=battles.Values.Where(b=>b.FinishedAt is null&&b.Phase!="Cancelled")
            .OrderBy(b=>b.Phase=="Warning"?1:0).ThenBy(b=>b.ScheduledAt)
            .ThenBy(b=>due.Single(r=>r.Id==b.Id).Wave).ThenBy(b=>b.Id).FirstOrDefault();
        foreach(var raid in due)
        {
            if(raid.Outcome!=RaidOutcome.Pending)continue;
            if(village.DefeatedAt is not null)
            {
                if(battles.TryGetValue(raid.Id,out var defeatedBattle)){RaidSimulation.Cancel(village,raid,defeatedBattle,citizens,now);updated.Add(defeatedBattle);}
                else{raid.Outcome=RaidOutcome.Cancelled;raid.ResolvedAt=now;}
                changed=true;continue;
            }
            if(raid.EngineVersion < 2) { reports.Add(RaidRules.Resolve(village,raid,citizens,completedProjects,Random.Shared,now)); continue; }
            if(!battles.TryGetValue(raid.Id,out var battle))
                throw new InvalidOperationException("Live raid has no persisted battle.");
            if(battle.Id!=allowed?.Id)
            {
                if(battle.Phase=="Warning"&&!battle.Deferred)
                {battle.Deferred=true;battle.Revision++;changed=true;updated.Add(battle);}
                continue;
            }
            if(battle.Deferred)
            {
                battle.Deferred=false;battle.StartsAt=now;battle.LastTickAt=now;battle.EndsAt=now.AddSeconds(battle.Settings?.DurationSeconds??240);raid.StrikesAt=now;
                battle.Revision++;changed=true;updated.Add(battle);
                foreach(var unit in battle.Units)unit.NextAttackAt=now.AddSeconds(2);
                var priorRaid=village.Raids.Where(r=>r.Id!=raid.Id&&r.AnnouncedAt==raid.AnnouncedAt&&r.Wave<raid.Wave).OrderByDescending(r=>r.Wave).FirstOrDefault();
                if(priorRaid is not null)
                {
                    var prior=await db.RaidBattles.SingleOrDefaultAsync(b=>b.Id==priorRaid.Id,cancellationToken);
                    if(prior is not null)CarryFighters(prior,battle,now);
                }
            }
            else if(battle.Phase=="Warning"&&battle.Fighters.Count==0&&raid.Wave>1)
            {
                var priorRaid=village.Raids.Where(r=>r.Id!=raid.Id&&r.AnnouncedAt==raid.AnnouncedAt&&r.Wave<raid.Wave).OrderByDescending(r=>r.Wave).FirstOrDefault();
                if(priorRaid is not null)
                {
                    var prior=await db.RaidBattles.SingleOrDefaultAsync(b=>b.Id==priorRaid.Id,cancellationToken);
                    if(prior is not null)CarryFighters(prior,battle,now);
                }
            }
            if(HasHospitalRestriction(village.OwnerAccountId))
                foreach(var royal in battle.Fighters.Where(f=>f.IsTsar))royal.Active=false;
            var advanced = RaidSimulation.Tick(village,raid,battle,citizens,
                citizens.Where(c=>presence.IsPresent(c.Id)).Select(c=>c.Id).ToHashSet(),now,
                sample=>CombatDiagnostics.Record(logger,village.Id,battle.Id,now,battle.Units.Count(u=>u.Alive),battle.Fighters.Count(f=>f.Active&&f.Health>0),sample));
            changed |= advanced;
            if(advanced&&!updated.Contains(battle))updated.Add(battle);
            if(battle.FinishedAt is not null)
                reports.Add(BattleReport(raid,battle,citizens));
        }
        if(village.DefeatedAt is not null)
        {
            foreach(var pendingRaid in village.Raids.Where(r=>r.Outcome==RaidOutcome.Pending))
            {
                var queued=await db.RaidBattles.SingleOrDefaultAsync(b=>b.Id==pendingRaid.Id,cancellationToken);
                if(queued is not null){RaidSimulation.Cancel(village,pendingRaid,queued,citizens,now);updated.Add(queued);}
                else{pendingRaid.Outcome=RaidOutcome.Cancelled;pendingRaid.ResolvedAt=now;}
            }
            RaidRules.UpdateNextRaidAt(village);
        }
        var restChanged = new HashSet<Guid>();
        var waitingForRest = citizens.Where(c => c.RestState == RestState.RestRequested && c.CycleSettled).ToArray();
        if (waitingForRest.Length > 0)
        {
            var activeBattles = await db.RaidBattles.Where(b => b.VillageId == village.Id && b.FinishedAt == null).ToListAsync(cancellationToken);
            foreach (var citizen in waitingForRest)
            {
                var blocked = activeBattles.Any(b => b.Fighters.Any(f => !f.IsTsar && f.CitizenId == citizen.Id && f.Active && f.Health > 0));
                SafeRest.AfterSettlement(citizen, now, blocked);
                if (citizen.RestState == RestState.Resting) { restChanged.Add(citizen.Id); changed = true; }
            }
        }
        var worldAfter=(village.BridgeOpened,village.BridgeRuinedAt,village.DefeatedAt,
            Ruins:CombatRules.Buildings(village).Count(b=>b.Damage>=100));
        return new RaidsFought(reports,citizens.Where(c=>restChanged.Contains(c.Id)||updated.Any(b=>b.FinishedAt is not null&&b.Fighters.Any(f=>!f.IsTsar&&f.CitizenId==c.Id)) || reports.Any(r=>r.Defenders.Any(d=>d.CitizenId==c.Id)||r.Victims.Any(v=>v.CitizenId==c.Id))
            || changed && (!injuredBefore.Contains(c.Id) && CombatRules.IsInjured(c,now)
                || peopleBefore[c.Id]!=(c.HomePlotId,c.CurrentSiteId,c.CycleSettled,c.HospitalizedAt is not null?c.IdleSpot:null)))
            .ToArray(),changed,updated,village.Fortifications,worldBefore!=worldAfter);
    }

    private async Task BroadcastRaidsAsync(Village village, RaidsFought raids, DateTimeOffset now)
    {
        foreach(var battle in raids.Battles ?? []) await notifier.BattleUpdated(village.Slug,battle,raids.Walls ?? []);
        foreach (var report in raids.Reports)
        {
            logger.RaidFought(village.Slug, report.Count, report.Enemy.ToString(), report.Repelled, report.Defenders.Count);
            await notifier.RaidFought(village.Slug, report);
        }

        foreach (var citizen in raids.Citizens)
        {
            await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
        }
    }

    /// <summary>The idle citizen takes a good out of one storage building into their hands.</summary>
    public async Task<CitizenDto> TakeFromStorageAsync(string slug, Guid accountId, TakeRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            await SyncHospitalAsync(db, key.VillageId, cancellationToken);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            HospitalRules.RequireReady(citizen, time.GetUtcNow());
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            StorageRules.Take(citizen, village, request.PlotId, request.Resource, request.Amount, now, request.Sharpen, request.Durability);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "take", $"{request.Amount} {request.Resource} from {request.PlotId}");

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The citizen stops the running task, job or research; see <see cref="CycleEngine.Cancel"/>.</summary>
    public async Task<CitizenDto> CancelTaskAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            await SyncHospitalAsync(db, key.VillageId, cancellationToken);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            HospitalRules.RequireReady(citizen, time.GetUtcNow());
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            var raids = await FightDueRaidsAsync(db, village, null, now, cancellationToken);
            if(citizen.RescuePatientId is {} patientId)
            {
                var patient=await db.Citizens.SingleOrDefaultAsync(c=>c.Id==patientId&&c.VillageId==village.Id,cancellationToken);
                RescueRules.Cancel(citizen,patient,now,village);
                if(village.TsarRescueByCitizenId==citizen.Id)
                {village.TsarSpot=citizen.IdleSpot;village.TsarRescueByCitizenId=null;}
            }
            CycleEngine.Cancel(citizen, now, village);
            await FinishWorkOrderAsync(db, village, citizen, false, true, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "cancelTask", "");

            await BroadcastRaidsAsync(village, raids, now);
            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>"Come here": the idle citizen walks to a spot on the land and waits there for the next task.</summary>
    public async Task<CitizenDto> GoToAsync(string slug, Guid accountId, GoToRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            await SyncHospitalAsync(db, key.VillageId, cancellationToken);
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            db.AuditActor = citizen.AccountId;
            HospitalRules.RequireReady(citizen, time.GetUtcNow());
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            village.EnsureCamp(now);
            await RequireMovementReadyAsync(db,village.Id,accountId,citizen.Id,cancellationToken);
            citizen.GoTo(request.Spot, village, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "goTo", request.Spot.ToString());

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DueCitizen>> FindDueCitizensAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        return await db.Citizens
            .Where(c => c.RecoveryPausedAt == null && !c.CycleSettled && c.CycleEndsAt <= now)
            .OrderBy(c => c.CycleEndsAt)
            .Select(c => new DueCitizen(c.VillageId, c.Id))
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Banks the finished cycles of some citizens of one village, one by one, then marks the village state for sending once.
    /// The village lock is taken per <see cref="SettlementChunkSize"/> citizens, so player actions wait for one chunk,
    /// not for the whole batch.
    /// </summary>
    public async Task SettleVillageAsync(Guid villageId, IReadOnlyCollection<Guid> citizenIds, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var settled = 0;
        foreach (var chunk in citizenIds.Chunk(SettlementChunkSize))
        {
            using var _ = await AcquireLockAsync(villageId, cancellationToken);
            foreach (var citizenId in chunk)
            {
                SettlementResult result;
                try
                {
                    // A corrupt/failed cycle must never roll back work already earned by other residents.
                    result = await WithRetryAsync(async db =>
                    {
                        var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
                        return await SettleOneAsync(db, village, citizenId, cancellationToken);
                    }, cancellationToken, villageId);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    logger.LogError(error, "Cycle settlement failed for one citizen {CitizenId} in village {VillageId}.", citizenId, villageId);
                    continue;
                }
                if (result.Settled) { settled++; changes.Mark(villageId); }
                // No notification is emitted until the individual financial/progress transaction committed.
                if (result.Publish is not null)
                {
                    try { await result.Publish(); }
                    catch (Exception error) when (error is not OperationCanceledException)
                    { logger.LogWarning("Committed cycle notification failed ({Type}).", error.GetType().Name); }
                }
            }
        }
        logger.VillageSettled(settled, citizenIds.Count, villageId, (long)time.GetElapsedTime(started).TotalMilliseconds);
    }

    private sealed record SettlementResult(bool Settled, Func<Task>? Publish = null);

    /// <summary>The village state for viewers, or null when the village is gone.</summary>
    public async Task<VillageStateDto?> GetVillageStateAsync(Guid villageId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var village = await db.Villages.AsNoTracking().SingleOrDefaultAsync(v => v.Id == villageId, cancellationToken);
        return village is null ? null : await BuildStateAsync(db, village, time.GetUtcNow(), cancellationToken);
    }

    private async Task<SettlementResult> SettleOneAsync(DesertodDbContext db, Village village, Guid citizenId, CancellationToken cancellationToken)
    {
        await SyncHospitalAsync(db, village.Id, cancellationToken);
        var citizen = await db.Citizens.SingleAsync(c => c.Id == citizenId, cancellationToken);
        var now = time.GetUtcNow();
        var resumed=HospitalRules.Resume(citizen,now);
        if (citizen.RecoveryPausedAt is not null || citizen.CycleSettled || citizen.VillageId != village.Id)
        {
            return new(false);
        }

        village.EnsureCamp(now);
        var context = (await LoadContextAsync(db, village, citizen.Id, now, cancellationToken)).Work;
        var raids = await FightDueRaidsAsync(db, village, context.CompletedProjects, now, cancellationToken);
        var report = await SettleWorkAsync(db, citizen, context, now, cancellationToken);
        if (report is null)
        {
            if (raids.Reports.Count > 0 || raids.Changed || resumed)
            {
                await db.SaveChangesAsync(cancellationToken);
                if(raids.Reports.Count>0)changes.Mark(village.Id);
                return new(false, async () => {
                    await BroadcastRaidsAsync(village, raids, now);
                    if(resumed)await notifier.CitizenUpdated(village.Slug,citizen.ToSummaryDto(now));
                });
            }

            return new(false);
        }

        if(GameCatalog.FindTask(report.TaskId)?.Effect==TaskEffect.Guard)
        {citizen.CurrentTaskId=null;citizen.DefenseSector=null;citizen.DefenseReadyAt=null;}
        await AfterSettleAsync(db, village, report, now, cancellationToken);
        var completion = await CompleteProjectIfDoneAsync(db, context, now, cancellationToken);
        var quests = await UpdateQuestsAsync(db, village, citizen, report, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.CycleSettled(
                citizen.Id,
                village.Slug,
                report.TaskId,
                FormatAmounts(report.Produced),
                FormatAmounts(report.Carried),
                report.ProjectContribution,
                report.RareFind);
        }

        return new(true, async () =>
        {
            await BroadcastRaidsAsync(village, raids, now);
            await BroadcastEventsAsync(village, citizen, report, completion, quests, now);
        });
    }

    private static string FormatAmounts(IEnumerable<ResourceAmount> amounts) =>
        string.Join(", ", amounts.Select(amount => $"{amount.Amount} {amount.Resource}"));

    private async Task<ProjectCompletion?> CompleteProjectIfDoneAsync(
        DesertodDbContext db,
        WorkContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.Project is not { Status: ProjectStatus.Active } project) return null;
        if (project.StagedBridge)
        {
            var finished = (project.SurveyDone ? 1 : 0) + VillageProject.BridgeEnds.Count(end => project.BuildProgress >= end);
            while (project.RewardedStages < finished)
            {
                var index = project.RewardedStages - 1;
                var weights=index<0 ? project.Contributions.Where(c=>c.Key==VillageProject.SurveyKey).ToDictionary(c=>c.CitizenId,c=>(double)c.Amount)
                    : project.BridgeHands.Where(h=>h.StartsWith($"{index}:")).GroupBy(h=>Guid.Parse(h.Split(':')[1])).ToDictionary(g=>g.Key,g=>(double)g.Count());
                var ids=weights.Keys.ToArray();
                var recipients = await db.Citizens.Where(c=>ids.Contains(c.Id)).ToListAsync(cancellationToken);
                var eligible=weights.Where(kv=>recipients.Any(c=>c.Id==kv.Key)).ToDictionary(kv=>kv.Key,kv=>kv.Value);
                var shares=ProjectRules.PoolShares(eligible,5);
                foreach(var recipient in recipients)recipient.Renown+=shares.GetValueOrDefault(recipient.Id);
                project.RewardedStages++;
            }
        }
        if (!project.IsComplete) return null;

        var contributorIds = project.Contributions.Select(c => c.CitizenId).Distinct().ToList();
        var contributors = await db.Citizens
            .Where(c => contributorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return ProjectRules.Complete(project, context.Village, contributors, now);
    }

    /// <summary>
    /// Credits the finished cycle, opens quests for the village's current problems and completes the solved ones.
    /// <paramref name="citizen"/> is the one being settled: its hunger is not saved yet, so it is counted from memory.
    /// </summary>
    private async Task<IReadOnlyList<QuestCompletion>> UpdateQuestsAsync(
        DesertodDbContext db,
        Village village,
        Citizen? citizen,
        CycleReport? report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await db.VillageQuests
            .Include(q => q.Contributions)
            .Where(q => q.VillageId == village.Id && q.Status == QuestStatus.Active)
            .ToListAsync(cancellationToken);

        var settlingId = citizen?.Id ?? Guid.Empty;
        var counts = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .GroupBy(_ => 1)
            .Select(g => new { Active = g.Count(), OtherHungry = g.Count(c => c.IsHungry && c.Id != settlingId) })
            .SingleOrDefaultAsync(cancellationToken);
        var needs = new VillageNeeds(
            counts?.Active ?? 0,
            (counts?.OtherHungry ?? 0) + (citizen is { IsHungry: true } ? 1 : 0));

        if (report is not null)
        {
            QuestRules.Credit(village, active, report);
        }

        foreach (var opened in QuestRules.Open(village, active, needs, report, now))
        {
            logger.QuestOpened(village.Slug, opened.Kind.ToString());
            db.VillageQuests.Add(opened);
        }

        var storyDone = await db.VillageQuests
            .Where(q => q.VillageId == village.Id && q.Status == QuestStatus.Completed)
            .Select(q => q.Kind)
            .Distinct()
            .ToListAsync(cancellationToken);
        var story = StoryQuests.Open(village, active, storyDone, now);
        if (story is not null)
        {
            logger.QuestOpened(village.Slug, story.Kind.ToString());
            db.VillageQuests.Add(story);
        }

        // Tracked entities first: a project finished in this transaction is not saved yet.
        var projects = await db.VillageProjects
            .Where(p => p.VillageId == village.Id)
            .ToListAsync(cancellationToken);
        var completedProjects = projects.Where(p => p.Status == ProjectStatus.Completed).Select(p => p.DefinitionId).ToHashSet();
        var running = projects.SingleOrDefault(p => p.Status == ProjectStatus.Active);
        var storyQuests = story is null ? active : [.. active, story];
        if (StoryQuests.ProjectToStart(storyQuests, running, completedProjects) is { } project)
        {
            var started = VillageProject.Start(village.Id, project, now);
            logger.ProjectStarted(village.Slug, started.DefinitionId);
            db.VillageProjects.Add(started);
        }

        var solved = active.Where(q => QuestRules.IsComplete(q, village, needs.ActiveCitizens, completedProjects)).ToList();
        if (solved.Count == 0)
        {
            return [];
        }

        var contributorIds = solved.SelectMany(q => q.Contributions).Select(c => c.CitizenId).Distinct().ToList();
        var contributors = await db.Citizens
            .Where(c => contributorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        return solved.Select(q => QuestRules.Complete(q, village, contributors, now)).ToList();
    }

    /// <summary>Everything about one citizen's cycle except the village state, which callers send once.</summary>
    private async Task BroadcastEventsAsync(
        Village village,
        Citizen citizen,
        CycleReport? report,
        ProjectCompletion? completion,
        IReadOnlyList<QuestCompletion> quests,
        DateTimeOffset now)
    {
        if (report is not null)
        {
            await notifier.CycleCompleted(village.Slug, report);
        }

        if (completion is not null)
        {
            logger.ProjectCompleted(village.Slug, completion.ProjectId);
            await notifier.ProjectCompleted(village.Slug, completion);
        }

        foreach (var quest in quests)
        {
            logger.QuestCompleted(village.Slug, quest.Kind.ToString());
            await notifier.QuestCompleted(village.Slug, quest);
        }

        await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
    }

    private static async Task<VillageContext> LoadContextAsync(
        DesertodDbContext db,
        Village village,
        Guid? citizenId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var project = await db.VillageProjects
            .Include(p => p.Materials)
            .Include(p => p.Contributions)
            .SingleOrDefaultAsync(p => p.VillageId == village.Id && p.Status == ProjectStatus.Active, cancellationToken);

        var completedProjects = await db.VillageProjects
            .Where(p => p.VillageId == village.Id && p.Status == ProjectStatus.Completed)
            .OrderBy(p => p.CompletedAt)
            .Select(p => p.DefinitionId)
            .ToListAsync(cancellationToken);
        Fortifications.Ensure(village,completedProjects);

        // Active citizens, plus anyone still in a cycle whatever their status: working counts include them.
        var citizens = await db.Citizens.AsNoTracking()
            .Where(c => c.VillageId == village.Id
                        && (c.Status == CitizenStatus.Active || (!c.CycleSettled && c.CycleEndsAt > now)))
            .Select(c => new
            {
                c.Id,
                IsActive = c.Status == CitizenStatus.Active,
                c.CycleStartedAt,
                IsWorking = !c.CycleSettled && c.CycleEndsAt > now && c.RecoveryPausedAt == null && !(c.InjuredUntil > now) && !(c.ExhaustedUntil > now),
                c.CurrentTaskId,
                c.Stats.Intelligence,
                c.Skills,
            })
            .ToListAsync(cancellationToken);

        var projectTaskIds = GameCatalog.Tasks.Where(task => CycleEngine.ProjectWorkKey(task) is not null).Select(task => task.Id).ToArray();
        var farmTaskIds=GameCatalog.Tasks.Where(FarmRules.IsFarmWork).Select(task=>task.Id).ToArray();
        var unfinished = await db.Citizens.AsNoTracking()
            .Where(c => c.VillageId == village.Id && !c.CycleSettled && c.CurrentTechnologyId == null
                && (c.CurrentSiteId != null || projectTaskIds.Contains(c.CurrentTaskId!) || farmTaskIds.Contains(c.CurrentTaskId!)))
            .ToListAsync(cancellationToken);
        foreach (var entry in db.ChangeTracker.Entries<Citizen>().Where(e => e.Entity.VillageId == village.Id))
        {
            unfinished.RemoveAll(c => c.Id == entry.Entity.Id);
            if (!entry.Entity.CycleSettled) unfinished.Add(entry.Entity);
        }
        var siteWork = new List<SiteWorkClaim>();
        foreach (var c in unfinished)
        {
            if (c.CurrentTechnologyId is not null) continue;
            if (c.CurrentSiteId is { } plot && c.CurrentSiteStage is { } stage && c.CurrentTaskId is { } job)
                siteWork.Add(new SiteWorkClaim(c.Id, plot, stage, job, c.ReservedSiteWork));
            else if (project is not null && (c.ReservedProjectId is null || c.ReservedProjectId == project.Id)
                && GameCatalog.FindTask(c.CurrentTaskId ?? "") is { } task && CycleEngine.ProjectWorkKey(task) is { } key)
                siteWork.Add(new SiteWorkClaim(c.Id, project.Id.ToString(), 0, key, c.ReservedSiteWork));
        }

        var recentSince = now - GameRules.RecentActivityWindow;
        var working = citizens.Where(c => c.IsWorking).ToList();
        Citizen? rescuePatient = null;
        var rescuer = db.Citizens.Local.FirstOrDefault(c => c.Id == citizenId);
        if (rescuer?.RescuePatientId is { } rescueId)
            rescuePatient = await db.Citizens.SingleOrDefaultAsync(c => c.Id == rescueId && c.VillageId == village.Id, cancellationToken);
        var work = new WorkContext
        {
            Village = village,
            RescuePatient = rescuePatient,
            SiteWork = siteWork,
            FarmWork=unfinished.Where(c=>farmTaskIds.Contains(c.CurrentTaskId??""))
                .Select(c=>new FarmWorkClaim(c.Id,c.CyclePrivateOutput ? c.AccountId : null,c.CycleFarmTargets)).ToArray(),
            Project = project,
            CompletedProjects = completedProjects.ToHashSet(),
            ActiveCitizenCount = Math.Max(1, citizens.Count(c => c.IsActive && c.CycleStartedAt >= now.AddHours(-2))),
            RecentCitizenCount = Math.Max(1, citizens.Count(c => c.IsActive && c.CycleStartedAt >= recentSince)),
            SupervisionCapacity = working
                .Where(c => c.CurrentTaskId == SuperviseTaskId)
                .Sum(c => ProjectRules.SupervisionCapacity(c.Skills.SingleOrDefault(s => s.Kind == Skill.Building)?.Level ?? 0)),
            BuildersWorking = working.Count(c => c.CurrentTaskId == BuildTaskId && c.Id != citizenId),
        };
        return new VillageContext(work, completedProjects, citizens.Where(c => c.IsActive).Select(c => c.Id).ToList());
    }

    // Call only after saving all action mutations. Removing a JSON-owned site shifts the
    // ordinals of its surviving Progress/Hands; EF cannot save that tracked graph twice.
    // Reload within the same transaction before shelter assignment and DTO construction.
    private async Task<VillageStateDto> BuildSavedStateAsync(
        DesertodDbContext db, string slug, DateTimeOffset now, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        return await BuildStateAsync(db,await LoadVillageAsync(db,slug,cancellationToken),now,cancellationToken);
    }

    private async Task<VillageStateDto> BuildStateAsync(
        DesertodDbContext db,
        Village village,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var residents = await db.Citizens.Where(c=>c.VillageId==village.Id&&c.Status==CitizenStatus.Active).ToListAsync(cancellationToken);
        ShelterRules.Assign(village,residents,now);
        await db.SaveChangesAsync(cancellationToken);
        var context = await LoadContextAsync(db, village, null, now, cancellationToken);
        var electorate = context.ActiveCitizenIds.Count(presence.IsPresent);
        var quests = await db.VillageQuests
            .Include(q => q.Contributions)
            .Where(q => q.VillageId == village.Id && q.Status == QuestStatus.Active)
            .OrderBy(q => q.StartedAt)
            .ToListAsync(cancellationToken);
        var owner = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == village.OwnerAccountId)
            .Select(a => new { a.DisplayName, a.Character })
            .SingleAsync(cancellationToken);
        var tsar = new TsarDto(owner.Character?.Name ?? owner.DisplayName, owner.Character?.Appearance, village.TsarSpot, village.TsarInjuredUntil)
            { Journey=WalkJourneyDto.FromTrip(village.TsarTrip,now), RestState=village.TsarRestState, RestRequestedAt=village.TsarRestRequestedAt, AccountId=village.OwnerAccountId, HospitalizedAt=village.TsarHospitalizedAt, RescueByCitizenId=village.TsarRescueByCitizenId, RescueRequestedAt=village.TsarRescueRequestedAt };
        var trips = await LoadTripsAsync(db, village.Id, cancellationToken);
        var shownSince = now - DungeonRules.ShownAfterEnd;
        var dungeon = await db.DungeonRuns.AsNoTracking()
            .Where(r => r.VillageId == village.Id && (r.Phase != DungeonPhase.Ended || r.EndedAt >= shownSince))
            .OrderByDescending(r => r.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var siteOwners=village.Sites.Select(s=>s.OwnerAccountId??s.PlacedByAccountId).Concat(village.Structures.Select(s=>s.OwnerAccountId)).Where(id=>id!=null).Distinct().ToArray();
        var ownerNames=await db.Accounts.Where(a=>siteOwners.Contains(a.Id)).ToDictionaryAsync(a=>a.Id,a=>a.DisplayName,cancellationToken);
        var state=village.ToStateDto(context.Work, context.CompletedInOrder, electorate, quests, tsar, trips, dungeon, now);
        return state with
        {
            WorkContractsEnabled=await db.KingdomEconomies.Where(e=>e.VillageId==village.Id).Select(e=>e.WorkContractsEnabled).SingleOrDefaultAsync(cancellationToken),
            Sites=state.Sites.Select(s=>s with {OwnerName=(s.OwnerAccountId??s.PlacedByAccountId) is {} id
                ? residents.FirstOrDefault(c=>c.AccountId==id)?.Name??ownerNames.GetValueOrDefault(id):null}).ToArray(),
            Battles = (await db.RaidBattles.AsNoTracking().Where(b=>b.VillageId==village.Id && (b.FinishedAt==null || b.FinishedAt>=now.AddSeconds(-15))).OrderBy(b=>b.StartsAt).ToListAsync(cancellationToken)).Select(b=>b.ToBattleDto()).ToArray(),
            Fortifications = village.Fortifications,
            FortificationUpgrade=FortificationUpgrades.Quote(village),
            KingdomArea = KingdomRules.Area(village), CastleLevel = KingdomRules.Level(village),
            NextKingdomArea=KingdomRules.Level(village)<3?KingdomRules.AreaForLevel(Math.Max(1,KingdomRules.Level(village))+1):null,
            CivicOrders = village.CivicOrders,
            DefeatedAt=village.DefeatedAt,
            Relocations=village.Structures.Where(h=>ShelterRules.Beds(h)>0).Select(h=>new RelocationDto(h.PlotId,
                residents.Count(c=>c.HomePlotId==h.PlotId),ShelterRules.Relocation(village,residents,h.PlotId).Count)).ToArray(),
            Housing = village.Structures.Where(s => ShelterRules.Beds(s) > 0).Select(s => new HousingDto(s.PlotId, ShelterRules.Beds(s),
                residents.Count(c => c.HomePlotId == s.PlotId), s.Level, s.Number, s.OwnerAccountId,
                s.OwnerAccountId is {} ownerId ? residents.FirstOrDefault(c=>c.AccountId==ownerId)?.Name ?? ownerNames.GetValueOrDefault(ownerId) : null, s.BuiltAt)).ToArray(),
            BridgeOpened = village.BridgeOpened,BridgeDamage=village.BridgeDamage,BridgeRuinedAt=village.BridgeRuinedAt,
            Project = context.Work.Project is {StagedBridge:true} bridge ? bridge.ToDto(context.Work) with
            { BridgeStage=bridge.BridgeStage, StageName=!bridge.SurveyDone ? "Разметка" : new[]{"Основания","Опоры","Пролёт","Настил","Приёмка","Мост открыт"}[bridge.BridgeStage] } : context.Work.Project?.ToDto(context.Work)
        };
    }

    /// <summary>Last walks of the village's citizens, those who left included: what they saw stays seen.</summary>
    private static async Task<IReadOnlyList<Trip>> LoadTripsAsync(DesertodDbContext db, Guid villageId, CancellationToken cancellationToken) =>
        await db.Citizens.AsNoTracking()
            .Where(c => c.VillageId == villageId && c.Trip != null)
            .Select(c => c.Trip!)
            .ToListAsync(cancellationToken);

    /// <summary>Runs a change to the village while no other change to it runs in this process.</summary>
    private async Task<T> WithVillageAsync<T>(Guid villageId, Func<DesertodDbContext, Task<T>> action, CancellationToken cancellationToken, [System.Runtime.CompilerServices.CallerMemberName] string operation = "change")
    {
        using var _ = await AcquireLockAsync(villageId, cancellationToken, operation);
        return await WithRetryAsync(action, cancellationToken, villageId, operation);
    }

    private async Task<IDisposable> AcquireLockAsync(Guid villageId, CancellationToken cancellationToken, [System.Runtime.CompilerServices.CallerMemberName] string operation = "change")
    {
        var started = time.GetTimestamp();
        var releaser = await locks.AcquireAsync(villageId, cancellationToken);
        var waited = time.GetElapsedTime(started);
        CombatDiagnostics.Stage(villageId,"lock",waited.TotalMilliseconds);
        if (waited >= SlowLockThreshold)
        {
            logger.SlowLock((long)waited.TotalMilliseconds, villageId);
        }

        return new TimedVillageLease(releaser, villageId, operation, time, waited.TotalMilliseconds);
    }

    private sealed class TimedVillageLease(IDisposable lease, Guid village, string operation, TimeProvider clock, double waitedMs) : IDisposable
    {
        private readonly long started=clock.GetTimestamp();
        private int disposed;
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;
            var held=clock.GetElapsedTime(started).TotalMilliseconds;
            lease.Dispose();
            CombatDiagnostics.Operation(village,operation,clock.GetUtcNow(),waitedMs,held);
        }
    }

    private async Task<T> WithRetryAsync<T>(Func<DesertodDbContext, Task<T>> action, CancellationToken cancellationToken, Guid? villageId = null, string operation = "change")
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            try
            {
                if (villageId is null) return await action(db);
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                using var notifications=changes.BeginBatch();
                // The existing in-process lock is not shared by another API instance or settler.
                var lockKey = villageId.Value.ToString();
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
                var before = await BeginAccountingAsync(db, villageId.Value, cancellationToken);
                var savesBeforeAction = db.SuccessfulSaves;
                var result = await action(db);
                await FinishAccountingAsync(db, villageId.Value, before, operation, db.SuccessfulSaves > savesBeforeAction, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                notifications.Commit();
                return result;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaximumAttempts)
            {
                logger.Retrying(attempt, MaximumAttempts, "a concurrency conflict");
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, cancellationToken);
            }
            catch (DbUpdateException exception) when (attempt < MaximumAttempts
                                                      && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two cycles opened the same quest at once; the retry finds the one that was saved.
                logger.Retrying(attempt, MaximumAttempts, "a unique violation");
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, cancellationToken);
            }
        }
    }

    private async Task<CitizenKey?> FindCitizenAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        return await db.Citizens
            .Where(c => c.AccountId == accountId && db.Villages.Any(v => v.Id == c.VillageId && v.Slug == slug))
            .Select(c => new CitizenKey(c.Id, c.VillageId, !c.CycleSettled && c.CycleEndsAt <= now))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>The player's citizen in the village, who is present from now on since they act.</summary>
    private async Task<CitizenKey> RequireCitizenAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await FindCitizenAsync(slug, accountId, cancellationToken)
                  ?? throw new NotFoundException("citizen.notInVillage", "You do not live in this village.");
        await SeeAsync(slug, key.Id);
        return key;
    }

    private async Task<Guid> FindVillageIdAsync(string slug, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Villages.Where(v => v.Slug == slug).Select(v => (Guid?)v.Id).SingleOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("village.notFound", $"Village \"{slug}\" was not found.", new Dictionary<string, object> { ["slug"] = slug });
    }

    private static async Task<Village> LoadVillageAsync(DesertodDbContext db, string slug, CancellationToken cancellationToken) =>
        await db.Villages.SingleOrDefaultAsync(v => v.Slug == slug, cancellationToken)
        ?? throw new NotFoundException("village.notFound", $"Village \"{slug}\" was not found.", new Dictionary<string, object> { ["slug"] = slug });

    private static string Slugify(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in Transliterate(name.ToLowerInvariant()))
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > 40)
        {
            slug = slug[..40].Trim('-');
        }

        return slug.Length == 0 ? "village" : slug;
    }

    private static string Transliterate(string text)
    {
        const string cyrillic = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        string[] latin =
        [
            "a", "b", "v", "g", "d", "e", "e", "zh", "z", "i", "y", "k", "l", "m", "n", "o", "p", "r", "s", "t", "u",
            "f", "h", "ts", "ch", "sh", "sch", "", "y", "", "e", "yu", "ya",
        ];

        var builder = new StringBuilder();
        foreach (var character in text)
        {
            var index = cyrillic.IndexOf(character);
            builder.Append(index >= 0 ? latin[index] : character.ToString());
        }

        return builder.ToString();
    }
}
