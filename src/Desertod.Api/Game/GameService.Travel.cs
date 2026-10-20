using Desertod.Domain;
using Desertod.Domain.Dungeons;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    private static async Task RequireMovementReadyAsync(DesertodDbContext db,Guid village,Guid actor,Guid? citizen,CancellationToken ct)
    {
        var battles=await db.RaidBattles.Where(b=>b.VillageId==village&&b.FinishedAt==null).ToListAsync(ct);
        if(battles.Any(b=>b.Fighters.Any(f=>f.Active&&f.Health>0&&(citizen is {} id?!f.IsTsar&&f.CitizenId==id:f.IsTsar&&f.AccountId==actor))))
            throw new GameRuleException("goto.retreatRequired","Retreat from combat before travelling.");
        var runs=await db.DungeonRuns.Where(r=>r.VillageId==village&&r.Phase!=DungeonPhase.Ended).ToListAsync(ct);
        if(runs.Any(r=>r.Heroes.Any(h=>h.AccountId==actor&&!h.Settled)))
            throw new GameRuleException("dungeon.busy","Return from the dungeon or leave its gathering first.");
    }
}
