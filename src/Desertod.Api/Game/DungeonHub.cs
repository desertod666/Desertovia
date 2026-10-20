using Desertod.Api.Auth;
using Desertod.Domain.Dungeons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Desertod.Api.Game;

/// <summary>
/// A dungeon run live: anyone signed in may watch; a hero's player sends where to walk, whom to strike and whom to heal.
/// The server decides everything else.
/// </summary>
[Authorize]
public sealed class DungeonHub(DungeonRuntime runtime, GameService game) : Hub
{
    public static string GroupName(Guid runId) => $"dungeon:{runId}";

    /// <summary>Starts sending snapshots of the run; returns its map and heroes, or null for an unknown run.</summary>
    public async Task<DungeonRunDto?> JoinRun(Guid runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(runId));
        var accountId = Context.User?.GetAccountId();
        var stored = await game.GetDungeonRunAsync(runId, accountId, Context.ConnectionAborted);
        if (stored is null || runtime.Find(runId) is not { } live)
        {
            return stored;
        }

        DungeonSnapshotDto? snapshot;
        lock (live.Gate)
        {
            snapshot = live.LastSnapshot ?? DungeonMapper.Snapshot(runId, live.Simulation, []);
        }

        return stored with { Heroes = live.Heroes, MyHeroId = accountId is { } id ? live.HeroOf(id) : null, Snapshot = snapshot };
    }

    public Task LeaveRun(Guid runId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(runId));

    public Task<bool> MoveTo(Guid runId, Guid heroId, double x, double y) =>
        Act(runId, heroId, simulation => simulation.MoveTo(heroId, x, y));

    public Task<bool> Attack(Guid runId, Guid heroId, int foeId) =>
        Act(runId, heroId, simulation => simulation.Attack(heroId, foeId));

    public Task<bool> Heal(Guid runId, Guid heroId, Guid allyId) =>
        Act(runId, heroId, simulation => simulation.Heal(heroId, allyId));

    private async Task<bool> Act(Guid runId, Guid heroId, Func<DungeonSimulation, bool> intent)
    {
        if (Context.User?.GetAccountId() is not { } accountId || runtime.Find(runId) is not { } live || !live.Owns(heroId, accountId) || game.HasHospitalRestriction(accountId))
        {
            return false;
        }

        if (!await game.CheckDungeonHospitalAsync(live.VillageId, accountId, Context.ConnectionAborted)) return false;
        if (live.Save is null)
        {
            lock (live.Gate) return intent(live.Simulation);
        }
        await live.Operation.WaitAsync(Context.ConnectionAborted);
        try
        {
            if (runtime.Find(runId) != live || game.HasHospitalRestriction(accountId)) return false;
            var previous = live.Save!;
            var simulation = DungeonSimulation.Restore(previous.Fight);
            if (simulation.IsOver || !intent(simulation)) return false;
            var next = previous with { Revision = previous.Revision + 1, Fight = simulation.Capture() };
            await game.SettleDungeonAsync(runId, live.VillageId, [], null, simulation.Slain, Context.ConnectionAborted, next);
            live.Commit(next, simulation, new DungeonRandom(next.RandomState), DungeonMapper.Snapshot(runId, simulation, []));
            return true;
        }
        finally { live.Operation.Release(); }
    }
}
