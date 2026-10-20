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
                var battles = await db.RaidBattles.Where(b => b.VillageId == village.Id && b.FinishedAt == null).ToListAsync(ct);
                if (battles.Any(b => b.Fighters.Any(f => (tsar ? f.IsTsar && f.AccountId == actor : !f.IsTsar && f.CitizenId == citizen!.Id) && f.Active && f.Health > 0)))
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
                else SafeRest.Request(citizen!, now);
            }
            else if (tsar) { village.TsarRestState = RestState.Active; village.TsarRestRequestedAt = null; }
            else SafeRest.Wake(citizen!);
            db.AuditActor = actor; db.AuditOperation = request.Resting ? "safe-rest" : "wake";
            await db.SaveChangesAsync(ct); changes.Mark(village.Id);
            if (citizen is not null) await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            return await BuildStateAsync(db, village, now, ct);
        }, ct);
}
