using System.Text.Json;
using Desertod.Domain.Dungeons;

namespace Desertod.Api.Game;

public sealed record DungeonSave(long Revision, DungeonCheckpoint Fight, ulong RandomState,
    IReadOnlyList<DungeonRunHeroDto> Heroes, IReadOnlyList<Guid> Settled)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };
    public string ToJson() => JsonSerializer.Serialize(this);

    public static DungeonSave Read(string json) => JsonSerializer.Deserialize<DungeonSave>(json, Options)
        ?? throw new InvalidOperationException("Empty dungeon save.");

    public DungeonStart Resume(DungeonRun run, string slug)
    {
        // Validate before exposing the run; corrupt or unsupported saves remain stored for repair.
        _ = DungeonSimulation.Restore(Fight);
        if (Fight.Definition.Id != run.DungeonId || Revision < 0)
            throw new InvalidOperationException("Dungeon save does not match its run.");
        var fighters = Fight.Heroes.Select(h => h.Fighter).ToArray();
        return new DungeonStart(run.Id, run.VillageId, slug, Fight.Definition, run.Seed,
            fighters, Heroes, fighters.ToDictionary(f => f.Id, f => f.AccountId), this);
    }
}
