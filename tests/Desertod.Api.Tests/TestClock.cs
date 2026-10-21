using Desertod.Domain.Rules;

namespace Desertod.Api.Tests;

/// <summary>
/// Real time plus an offset tests move forward, so a cycle or a raid that takes minutes is over at once.
/// Timers still tick in real time: the background workers keep polling as in production.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private readonly object sync = new();
    private long offsetTicks = SkipFreeEnergy(System.GetUtcNow()).Ticks;
    private DateTimeOffset? frozenSystemTime;

    public override DateTimeOffset GetUtcNow()
    {
        lock(sync)return (frozenSystemTime??System.GetUtcNow())+TimeSpan.FromTicks(offsetTicks);
    }

    /// <summary>Control phase boundaries explicitly: slow database requests must not move a hero past the gate under test.</summary>
    public IDisposable Freeze()
    {
        lock(sync)
        {
            if(frozenSystemTime is not null)throw new InvalidOperationException("The test clock is already frozen.");
            frozenSystemTime=System.GetUtcNow();return new FrozenClock(this);
        }
    }

    private sealed class FrozenClock(TestClock clock):IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            lock(clock.sync)
            {
                if(disposed)return;
                // Resume from the same logical instant, retaining explicit Advance calls.
                clock.offsetTicks-=(TimeProvider.System.GetUtcNow()-clock.frozenSystemTime!.Value).Ticks;
                clock.frozenSystemTime=null;disposed=true;
            }
        }
    }

    /// <summary>Only forward: every test in the collection shares the clock, and time never runs back in the game.</summary>
    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        lock(sync)offsetTicks+=by.Ticks;
    }

    /// <summary>Tests check energy spending, which a playtest window of free energy would hide.</summary>
    private static TimeSpan SkipFreeEnergy(DateTimeOffset now) =>
        GameRules.IsFreeEnergy(now) ? GameRules.FreeEnergyUntil - now : TimeSpan.Zero;
}
