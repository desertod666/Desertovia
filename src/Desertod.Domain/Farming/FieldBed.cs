namespace Desertod.Domain.Farming;

/// <summary>
/// One bed of a farm. <see cref="Growth"/> is stored at <see cref="UpdatedAt"/>; growth since then is calculated,
/// the same way herds come back.
/// </summary>
public sealed class FieldBed
{
    /// <summary>Plot of the farm the bed belongs to.</summary>
    public required string PlotId { get; set; }

    public int Index { get; set; }
    /// <summary>Legacy beds start at Empty; every sow/harvest creates another generation, including empty beds.</summary>
    public Guid GenerationId { get; set; }

    /// <summary>Null while the bed is empty.</summary>
    public CropKind? Crop { get; set; }

    /// <summary>0 when sown, 1 when ripe.</summary>
    public double Growth { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The bed is wet until then; null when it was never watered.</summary>
    public DateTimeOffset? WateredUntil { get; set; }
}
