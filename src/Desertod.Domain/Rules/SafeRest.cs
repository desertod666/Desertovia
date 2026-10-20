using Desertod.Domain.Entities;

namespace Desertod.Domain.Rules;

/// <summary>Explicit player choice, independent of presence, idle work and medical pauses.</summary>
public enum RestState { Active, RestRequested, Resting }

public static class SafeRest
{
    public static void RequireActive(RestState state)
    {
        if (state != RestState.Active)
            throw new GameRuleException("rest.activeRequired", "Leave safe rest before starting another action.");
    }

    public static void Request(Citizen citizen, DateTimeOffset now)
    {
        if (citizen.RestState != RestState.Active) return;
        citizen.RestRequestedAt = now;
        citizen.RestState = !citizen.CycleSettled && citizen.CurrentTaskId is not null
            ? RestState.RestRequested : RestState.Resting;
        if (citizen.RestState == RestState.Resting) StopWalking(citizen, now);
    }

    public static void AfterSettlement(Citizen citizen, DateTimeOffset now)
    {
        if (citizen.RestState != RestState.RestRequested || !citizen.CycleSettled) return;
        citizen.RestState = RestState.Resting;
        StopWalking(citizen, now);
    }

    public static void Wake(Citizen citizen)
    {
        citizen.RestState = RestState.Active;
        citizen.RestRequestedAt = null;
    }

    private static void StopWalking(Citizen citizen, DateTimeOffset now)
    {
        citizen.IdleSpot = citizen.Trip?.PositionAt(now) ?? citizen.Spot ?? citizen.IdleSpot;
        citizen.Spot = citizen.IdleSpot;
        citizen.Trip = null;
    }
}
