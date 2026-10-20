using Desertod.Domain.Catalog;

namespace Desertod.Domain.Farming;

public sealed record FarmWorker(Guid AccountId,string Name,DateTimeOffset StartedAt);

/// <summary>A public crop's useful-work snapshot. Financial rights are a separate, backed settlement.</summary>
public sealed record FarmCropSnapshot(Guid Id,string PlotId,int Index,CropKind Crop,int ContributionVersion,
    DateTimeOffset SownAt,IReadOnlyList<FarmContribution> Contributions,DateTimeOffset? HarvestedAt = null)
{
    public IReadOnlyList<ResourceAmount> Produced { get; init; } = [];
    public IReadOnlyList<ResourceAmount> Lost { get; init; } = [];
    public double YieldFactor { get; init; }
}

/// <summary>One durable history row per crop generation; survives removal of the bed or the citizen.</summary>
public sealed class FarmCropRecord
{
    public Guid Id { get; set; }
    public Guid VillageId { get; set; }
    public required string PlotId { get; set; }
    public int BedIndex { get; set; }
    public CropKind Crop { get; set; }
    public int ContributionVersion { get; set; }
    public DateTimeOffset SownAt { get; set; }
    public DateTimeOffset? HarvestedAt { get; set; }
    public double YieldFactor { get; set; }
    public List<FarmContribution> Contributions { get; set; } = [];
    public List<ResourceAmount> Produced { get; set; } = [];
    public List<ResourceAmount> Lost { get; set; } = [];
}
