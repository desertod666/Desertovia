using Desertod.Domain.Dungeons;
using Microsoft.AspNetCore.SignalR;

namespace Desertod.Api.Game;

/// <summary>
/// Runs the dungeons: closes gatherings and marches once a second, and ticks the fights ten times a second, sending
/// each run's snapshot to its viewers. Heroes who are out, and runs that are over, are settled in the database.
/// </summary>
public sealed class DungeonWorker(
    GameService game,
    DungeonRuntime runtime,
    IHubContext<DungeonHub> hub,
    TimeProvider time,
    ILogger<DungeonWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PhaseInterval = TimeSpan.FromSeconds(1);

    /// <summary>A late tick moves the fight by this much at most, so a stall does not teleport anyone.</summary>
    private const double LongestTickSeconds = 0.5;
    private readonly Dictionary<Guid, Task> settlements = [];
    private readonly Dictionary<Guid, Task> durableTicks = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var start in await game.RestoreDungeonsAsync(stoppingToken)) runtime.Start(start);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Restoring unfinished dungeon runs failed.");
        }

        // A village transaction (medical sync, marching or rewards) must never stop
        // the in-memory fights. Both loops are owned and awaited by this worker.
        using var loops = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var phases = AdvanceLoopAsync(loops.Token);
        var fights = FightLoopAsync(loops.Token);
        await Task.WhenAny(phases, fights);
        await loops.CancelAsync();
        await Task.WhenAll(phases, fights);
    }

    private async Task AdvanceLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PhaseInterval);
        do
        {
            await AdvancePhasesAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task FightLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        var lastTick = time.GetTimestamp();
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var seconds = Math.Min(LongestTickSeconds, time.GetElapsedTime(lastTick).TotalSeconds);
                lastTick = time.GetTimestamp();
                foreach (var id in settlements.Where(pair => pair.Value.IsCompleted).Select(pair => pair.Key).ToList())
                    settlements.Remove(id);
                foreach (var id in durableTicks.Where(pair => pair.Value.IsCompleted).Select(pair => pair.Key).ToList())
                    durableTicks.Remove(id);
                foreach (var live in runtime.Runs.ToList())
                {
                    if (live.Save is not null)
                    {
                        if (!durableTicks.ContainsKey(live.RunId))
                            durableTicks[live.RunId] = TickDurableAsync(live, seconds, stoppingToken);
                        continue;
                    }
                    try
                    {
                        await TickAsync(live, seconds, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogError(exception, "Dungeon run {RunId} failed to tick.", live.RunId);
                    }
                }
            }
        }
        finally
        {
            await Task.WhenAll(settlements.Values);
            await Task.WhenAll(durableTicks.Values);
        }
    }

    private async Task TickDurableAsync(LiveDungeon live, double seconds, CancellationToken cancellationToken)
    {
        if (!await live.Operation.WaitAsync(0, cancellationToken)) return;
        try
        {
            if (runtime.Find(live.RunId) != live) return;
            var tickTimestamp = time.GetTimestamp();
            if (live.LastTickTimestamp != 0)
                seconds = Math.Min(LongestTickSeconds, time.GetElapsedTime(live.LastTickTimestamp, tickTimestamp).TotalSeconds);
            var previous = live.Save!;
            // Mutate an unpublished copy. Failed persistence leaves the last committed fight intact.
            var simulation = DungeonSimulation.Restore(previous.Fight);
            var random = new DungeonRandom(previous.RandomState);
            foreach (var hero in simulation.Heroes)
                if (game.HasHospitalRestriction(hero.Fighter.AccountId)) simulation.WithdrawToHospital(hero.Id);
            var events = simulation.Tick(seconds, random);
            var exits = simulation.Heroes.Where(h => (h.State == HeroState.Left || simulation.IsOver)
                    && !previous.Settled.Contains(h.Id))
                .Select(h => new DungeonExit(h.Id, simulation.OutcomeOf(h))).ToArray();
            var next = previous with
            {
                Revision = previous.Revision + 1, Fight = simulation.Capture(), RandomState = random.State,
                Settled = previous.Settled.Concat(exits.Select(e => e.HeroId)).ToArray(),
            };
            DungeonOutcome? outcome = simulation.IsOver ? simulation.Outcome : null;
            await game.SettleDungeonAsync(live.RunId, live.VillageId, exits, outcome, simulation.Slain, cancellationToken, next);
            var snapshot = DungeonMapper.Snapshot(live.RunId, simulation, events);
            live.Commit(next, simulation, random, snapshot);
            live.LastTickTimestamp = tickTimestamp;
            if (outcome is not null) runtime.Remove(live.RunId);
            try
            {
                await hub.Clients.Group(DungeonHub.GroupName(live.RunId)).SendAsync("dungeonSnapshot", snapshot, cancellationToken);
                if (outcome is not null)
                    await hub.Clients.Group(DungeonHub.GroupName(live.RunId)).SendAsync("dungeonEnded", live.RunId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Dungeon {RunId} committed; reconnect will recover its snapshot.", live.RunId);
            }
        }
        catch (Exception exception)
        {
            if (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                logger.LogError(exception, "Dungeon {RunId} was not advanced; retained at its committed checkpoint for retry.", live.RunId);
        }
        finally { live.Operation.Release(); }
    }

    private async Task AdvancePhasesAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var start in await game.AdvanceDungeonsAsync(cancellationToken))
            {
                runtime.Start(start);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Advancing dungeon runs failed.");
        }
    }

    private async Task TickAsync(LiveDungeon live, double seconds, CancellationToken cancellationToken)
    {
        if (settlements.TryGetValue(live.RunId, out var pending) && pending.IsCompleted)
            settlements.Remove(live.RunId);
        var canSettle = !settlements.ContainsKey(live.RunId);
        var exits = new List<DungeonExit>();
        DungeonOutcome? outcome = null;
        int slain;
        DungeonSnapshotDto snapshot;
        lock (live.Gate)
        {
            var simulation = live.Simulation;
            foreach (var hero in simulation.Heroes)
                if (game.HasHospitalRestriction(hero.Fighter.AccountId)) simulation.WithdrawToHospital(hero.Id);
            var events = simulation.Tick(seconds, live.Random);
            foreach (var hero in simulation.Heroes)
            {
                if (canSettle && (hero.State == HeroState.Left || simulation.IsOver) && live.Settled.Add(hero.Id))
                {
                    exits.Add(new DungeonExit(hero.Id, simulation.OutcomeOf(hero)));
                }
            }

            if (canSettle && simulation.IsOver)
            {
                outcome = simulation.Outcome;
            }

            slain = simulation.Slain;
            snapshot = DungeonMapper.Snapshot(live.RunId, simulation, events);
            live.LastSnapshot = snapshot;
        }

        try
        {
            await hub.Clients.Group(DungeonHub.GroupName(live.RunId)).SendAsync("dungeonSnapshot", snapshot, cancellationToken);
        }
        catch
        {
            lock (live.Gate)
                foreach (var exit in exits) live.Settled.Remove(exit.HeroId);
            throw;
        }
        if (exits.Count == 0 && outcome is null)
        {
            return;
        }

        settlements[live.RunId] = SettleAsync(live, exits, outcome, slain, cancellationToken);
    }

    private async Task SettleAsync(LiveDungeon live, IReadOnlyList<DungeonExit> exits,
        DungeonOutcome? outcome, int slain, CancellationToken cancellationToken)
    {
        try
        {
            await game.SettleDungeonAsync(live.RunId, live.VillageId, exits, outcome, slain, cancellationToken);
        }
        catch (Exception exception)
        {
            // Failed persistence is retried on a later tick. Do not lose rewards or
            // remove the live run before its final transaction has actually committed.
            lock (live.Gate)
                foreach (var exit in exits) live.Settled.Remove(exit.HeroId);
            if (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                logger.LogError(exception, "Settling dungeon run {RunId} failed; retained for retry.", live.RunId);
            return;
        }

        if (outcome is not null)
        {
            runtime.Remove(live.RunId);
            try
            {
                await hub.Clients.Group(DungeonHub.GroupName(live.RunId)).SendAsync("dungeonEnded", live.RunId, cancellationToken);
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                    logger.LogWarning(exception, "Dungeon run {RunId} committed but its end notification failed.", live.RunId);
            }
        }
    }
}
