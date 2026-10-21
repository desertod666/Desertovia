using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    // A ruler may also have a citizen. Switching the political view must not
    // hide that account's existing body, including old fighters without AccountId.
    private static bool IsActor(RaidFighter fighter, Guid account, Guid? citizen) =>
        fighter.IsTsar ? fighter.AccountId == account : citizen is {} id && fighter.CitizenId == id;

    private static async Task<bool> HasActiveRaidActorAsync(DesertodDbContext db, Guid village, Guid account, Guid? citizen, CancellationToken ct)
    {
        citizen ??= await db.Citizens.Where(c => c.VillageId == village && c.AccountId == account)
            .Select(c => (Guid?)c.Id).SingleOrDefaultAsync(ct);
        var battles = await db.RaidBattles.Where(b => b.VillageId == village && b.FinishedAt == null).ToListAsync(ct);
        return battles.Any(b => b.Fighters.Any(f => f.Active && f.Health > 0 && IsActor(f, account, citizen)));
    }

    private static async Task RequireNoRaidActorAsync(DesertodDbContext db, Guid village, Guid account, Guid? citizen, CancellationToken ct)
    {
        if (await HasActiveRaidActorAsync(db, village, account, citizen, ct))
            throw new GameRuleException("goto.retreatRequired", "Retreat from combat before travelling.");
    }

    private static async Task RequireNoDungeonActorAsync(DesertodDbContext db, Guid village, Guid account, CancellationToken ct)
    {
        var runs = await db.DungeonRuns.Where(r => r.VillageId == village && r.Phase != DungeonPhase.Ended).ToListAsync(ct);
        if (runs.Any(r => r.Heroes.Any(h => h.AccountId == account && !h.Settled)))
            throw new GameRuleException("dungeon.busy", "Return from the dungeon or leave its gathering first.");
    }
}
