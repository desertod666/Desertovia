using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;

namespace Desertod.Domain.Rules;

/// <summary>A citizen leaves the village for good; the caller then deletes them.</summary>
public static class Departure
{
    /// <summary>
    /// Stops the running task and the dungeon run, and leaves behind gear and goods: as much as fits goes into storage,
    /// the rest is lost. What the citizen saw of the land stays seen.
    /// </summary>
    /// <param name="run">The run the citizen answered, if any.</param>
    public static void Leave(Citizen citizen, Village village, DungeonRun? run, DateTimeOffset now)
    {
        if(run?.Kind==DungeonRunKind.Ambush&&run.IsActive)throw new GameRuleException("ambush.retreatRequired","Leave the fight before leaving the village.");
        if (citizen.WorkState(now) == WorkState.Working)
        {
            CycleEngine.Cancel(citizen, now, village);
        }

        if (run is not null)
        {
            DungeonRules.Desert(run, village, citizen);
        }

        CombatRules.Disarm(citizen, village);
        StorageRules.Unload(citizen, village);
        if (citizen.Trip is { } trip)
        {
            village.Explore([trip], now);
        }
    }
}
