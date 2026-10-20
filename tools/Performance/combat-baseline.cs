#:project ../../src/Desertod.Api/Desertod.Api.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
// In-memory only: no URL, database, player accounts, messages or model calls.
// dotnet run -c Release tools/Performance/combat-baseline.cs > combat-baseline.json
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.World;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());
var wire = new JsonSerializerOptions(JsonSerializerDefaults.Web);
wire.Converters.Add(new JsonStringEnumConverter());
var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
const int seconds = 120;
// Warm the same simulation and DTO paths before recording. Not a load test or a live sizing promise.
Run(5); Run(20);
var profiles = new[] { 5, 10, 20 }.Select(Run).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new {
    schemaVersion = 1, capturedAt = DateTimeOffset.UtcNow,
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessors = Environment.ProcessorCount,
    scope = "single process synthetic current raid simulation and actual wire DTO serialization; no DB/network/rendering",
    simulatedSeconds = seconds, seed = "fixed world and raid ids; mixed west approach; active level2/3 residents",
    profiles,
    unmeasured = new[] {"database lock/save", "broadcast send", "network RTT", "browser frames", "other host services"},
    acceptance = "baseline observation only; PKG-D114 target and T081 capacity acceptance remain open"
}, json));

object Run(int count)
{
    var world = Village.Create("baseline", "Synthetic baseline", Guid.Parse("11111111-1111-1111-1111-111111111111"), now);
    world.Id = Guid.Parse("6e715c31-5387-4a7d-9c5e-201610150001");
    world.Wilds = Wilderness.Generate(world.Id);
    world.Wilds.Obstacles.Clear(); world.Wilds.Ravine.Clear(); world.Trees.Clear();
    var people = Enumerable.Range(1, count).Select(i => {
        var actor = Guid.Parse($"00000001-0000-0000-0000-{i:000000000000}");
        var c = CharacterCreation.Create(world.Id, actor, $"Synthetic{i}", now);
        c.Id = actor;
        c.Stats.Set(Stat.Strength, 2); c.Stats.Set(Stat.Dexterity, 2); c.Stats.Set(Stat.Endurance, 3);
        c.StoredEnergy = c.MaximumEnergy;
        c.CycleSettled = true; c.IdleSpot = new(15.5, 13.5);
        return c;
    }).ToArray();
    var online = people.Select(c => c.Id).ToHashSet();
    var raid = new Raid {Id = Guid.Parse("00000001-0000-0000-0000-000000000001"), Enemy = EnemyKind.Skeleton, AnnouncedAt = now, StrikesAt = now};
    world.Raids.Add(raid);
    var battle = RaidBalance.Prepare(world, raid, people, [], online, now, "west", "mixed");
    var units = battle.Units.Count;
    var ticks = new List<double>(); var simulation = new List<double>(); var serialization = new List<double>();
    var sizes = new List<int>(); var paths = new List<int>(); var nodes = new List<int>(); var steps = 0;
    var allocatedBefore = GC.GetTotalAllocatedBytes();
    using var process = Process.GetCurrentProcess(); var cpuBefore = process.TotalProcessorTime;
    for (var second = 1; second <= seconds && battle.FinishedAt is null; second++)
    {
        var started = Stopwatch.GetTimestamp();
        RaidSimulation.Tick(world, raid, battle, people, online, now.AddSeconds(second), metric => {
            simulation.Add(metric.ElapsedMs); paths.Add(metric.PathSearches); nodes.Add(metric.PathNodes); steps += metric.Steps;
        });
        ticks.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        started = Stopwatch.GetTimestamp();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(battle.ToBattleDto(), wire);
        serialization.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds); sizes.Add(bytes.Length);
    }
    process.Refresh();
    return new {onlineResidents = count, initialEnemies = units, ticks = ticks.Count, simulatedSteps = steps,
        fightersJoined = battle.Fighters.Count, battle.Phase, outcome = raid.Outcome.ToString(),
        tickMs = Distribution(ticks), simulationMs = Distribution(simulation), serializationMs = Distribution(serialization),
        payloadBytes = new {average = sizes.Average(), maximum = sizes.Max()},
        pathSearches = paths.Sum(), pathNodes = nodes.Sum(),
        processCpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds,
        allocatedBytes = GC.GetTotalAllocatedBytes() - allocatedBefore, workingSetBytes = process.WorkingSet64};
}
static object Distribution(List<double> values)
{
    var sorted = values.Order().ToArray();
    double At(double share) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * share) - 1)];
    return new {samples = sorted.Length, p50 = At(.5), p95 = At(.95), p99 = At(.99), maximum = sorted[^1]};
}
