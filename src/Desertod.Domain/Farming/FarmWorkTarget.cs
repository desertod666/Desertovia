namespace Desertod.Domain.Farming;

/// <summary>The bed and its generation accepted before work begins. Never retarget at settlement.</summary>
public sealed class FarmWorkTarget
{
    public required string PlotId { get; set; }
    public int Index { get; set; }
    public Guid GenerationId { get; set; }
}

public sealed record FarmWorkClaim(Guid CitizenId, Guid? OwnerAccountId, IReadOnlyList<FarmWorkTarget> Targets);
