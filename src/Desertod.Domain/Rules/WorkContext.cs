using Desertod.Domain.Entities;
using Desertod.Domain.Projects;

namespace Desertod.Domain.Rules;

/// <summary>Everything outside the citizen that a work cycle reads or changes.</summary>
public sealed class WorkContext
{
    public required Village Village { get; init; }
    public Citizen? RescuePatient { get; init; }
    /// <summary>Fresh combat admission checked under the same world transaction before settlement.</summary>
    public bool RestBlockedByCombat { get; set; }
    public bool SkipAmbush { get; set; }
    public Desertod.Domain.Combat.FightReport? ResolvedAmbush { get; set; }

    /// <summary>Active project with materials and all contributions loaded.</summary>
    public VillageProject? Project { get; init; }

    /// <summary>Ids of the village's finished projects; some tasks open only after one.</summary>
    public IReadOnlySet<string> CompletedProjects { get; init; } = new HashSet<string>();

    public int ActiveCitizenCount { get; init; } = 1;

    /// <summary>Citizens who started work within <see cref="GameRules.RecentActivityWindow"/>; sets the crew a stage needs.</summary>
    public int RecentCitizenCount { get; init; } = 1;

    /// <summary>Sum of laborers that Engineers currently on "supervise" can oversee.</summary>
    public int SupervisionCapacity { get; init; }

    /// <summary>Other citizens currently in a "build" cycle.</summary>
    public int BuildersWorking { get; init; }

    /// <summary>Persisted unfinished construction cycles, including overdue/offline/hospitalized citizens.</summary>
    public IReadOnlyList<SiteWorkClaim> SiteWork { get; init; } = [];
    public IReadOnlyList<Desertod.Domain.Farming.FarmWorkClaim> FarmWork { get; init; } = [];

    public int ReservedWork(string plotId, int stage, string jobId, int remaining, Guid? except = null)
    {
        var claims = SiteWork.Where(c => c.CitizenId != except && c.PlotId == plotId && c.Stage == stage && c.JobId == jobId).ToList();
        // Let old paid cycles finish under their old rules before assigning any new work on this job.
        return claims.Any(c => c.Amount is null) ? remaining : (int)Math.Min(remaining, claims.Sum(c => (long)(c.Amount ?? 0)));
    }

    public Random Random { get; init; } = Random.Shared;

    public static WorkContext For(Village village) => new() { Village = village };
}

public sealed record SiteWorkClaim(Guid CitizenId, string PlotId, int Stage, string JobId, int? Amount);
