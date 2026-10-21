using Desertod.Domain;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record RestRequest(bool Resting);

public sealed partial class GameService
{
    public async Task<VillageStateDto> SetRestAsync(string slug, Guid actor, RestRequest request, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct);
            var tsar = village.OwnerAccountId == actor;
            var citizen = tsar ? null : await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == actor && c.Status == CitizenStatus.Active, ct);
            if (!tsar && citizen is null) throw new UnauthorizedAccessException();
            var now = time.GetUtcNow();
            if (request.Resting)
            {
                if (await HasActiveRaidActorAsync(db,village.Id,actor,citizen?.Id,ct))
                    throw new GameRuleException("rest.retreatRequired", "Retreat from combat before entering safe rest.");
                var runs = await db.DungeonRuns.Where(r => r.VillageId == village.Id && r.Phase != DungeonPhase.Ended).ToListAsync(ct);
                if (runs.Any(r => r.Heroes.Any(h => h.AccountId == actor && !h.Settled)) || citizen?.DungeonRunId is not null)
                    throw new GameRuleException("rest.dungeonRequired", "Return from the dungeon or leave its gathering before resting.");
                if (tsar)
                {
                    village.TsarRestState = RestState.Resting;
                    village.TsarRestRequestedAt ??= now;
                    village.TsarSpot = village.TsarTrip?.PositionAt(now) ?? village.TsarSpot;
                    village.TsarTrip = null;
                }
                else { SafeRest.Request(citizen!, now); SafeRest.AfterSettlement(citizen!, now); }
            }
            else if (tsar) { village.TsarRestState = RestState.Active; village.TsarRestRequestedAt = null; }
            else SafeRest.Wake(citizen!);
            db.AuditActor = actor; db.AuditOperation = request.Resting ? "safe-rest" : "wake";
            await db.SaveChangesAsync(ct); changes.Mark(village.Id);
            if (citizen is not null) await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            return await BuildStateAsync(db, village, now, ct);
        }, ct);

    private async Task<CycleReport?> SettleWorkAsync(Desertod.Infrastructure.Data.DesertodDbContext db, Citizen citizen, WorkContext context, DateTimeOffset now, CancellationToken ct)
    {
        if(!await PrepareWorkEncounterAsync(db,citizen,context,now,ct))return null;
        if (citizen.RestState == RestState.RestRequested)
        {
            // A raid may have reached the worker after the rest request. Work completion
            // cannot remove an admitted fighter or grant immunity before retreat.
            context.RestBlockedByCombat = await HasActiveRaidActorAsync(db,citizen.VillageId,citizen.AccountId,citizen.Id,ct);
        }
        return CycleEngine.Settle(citizen, context, now);
    }
}
