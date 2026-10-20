using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;

namespace Desertod.Domain.Tests;

public sealed class DungeonCheckpointTests
{
    private static string Json(DungeonSimulation fight) => JsonSerializer.Serialize(fight.Capture());
    private static DungeonSimulation RoundTrip(DungeonSimulation fight) => DungeonSimulation.Restore(
        JsonSerializer.Deserialize<DungeonCheckpoint>(Json(fight))!);

    [Fact]
    public void Restart_preserves_full_precision_paths_cooldowns_arrows_loot_and_random_sequence()
    {
        var fighter = new DungeonFighter(Guid.NewGuid(), Guid.NewGuid(), "Archer", HeroClass.Archer, 100000, 70, .3, 200, false);
        var original = new DungeonSimulation(DungeonCatalog.Get(DungeonCatalog.DeadLands), [fighter]);
        var random = DungeonRandom.FromSeed(86);
        original.MoveTo(fighter.Id, 5.15, 5.23);
        for (var i = 0; i < 17; i++) original.Tick(.073, random);
        var restored = RoundTrip(original);
        var restoredRandom = new DungeonRandom(random.State);
        Assert.Equal(Json(original), Json(restored));
        for (var i = 0; i < 8000 && !original.IsOver; i++)
        {
            var foe = original.Foes.FirstOrDefault(f => f.IsAlive);
            if (foe is not null)
            {
                original.Attack(fighter.Id, foe.Id);
                restored.Attack(fighter.Id, foe.Id);
            }
            Assert.Equal(JsonSerializer.Serialize(original.Tick(.1, random)), JsonSerializer.Serialize(restored.Tick(.1, restoredRandom)));
            Assert.Equal(Json(original), Json(restored));
            Assert.Equal(random.State, restoredRandom.State);
        }
        Assert.Equal(DungeonOutcome.Victory, original.Outcome);
        Assert.Equal(2, original.Floor);
        Assert.NotEmpty(original.Heroes[0].Loot);
        Assert.True(original.Heroes[0].Arrows < 200);
    }

    [Fact]
    public void Capture_is_detached_and_preserves_healing_withdrawal_and_timeout()
    {
        var healer = new DungeonFighter(Guid.NewGuid(), Guid.NewGuid(), "Healer", HeroClass.Healer, 100, 1, 0, null, true);
        var ally = healer with { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Name = "Ally", CanHeal = false };
        var fight = new DungeonSimulation(DungeonCatalog.Parse("test", "Test", ["TTTTTTTT", "E....r.T", "TTTTTTTT"], TimeSpan.FromSeconds(1)), [healer, ally]);
        fight.Hero(ally.Id)!.Health = 50;
        Assert.True(fight.Heal(healer.Id, ally.Id));
        fight.Tick(.1, DungeonRandom.FromSeed(2));
        Assert.True(fight.Hero(healer.Id)!.Healed);
        var before = fight.Capture();
        fight.Hero(healer.Id)!.Loot[ResourceKind.Food] = 3;
        Assert.Empty(before.Heroes[0].Loot);
        fight.WithdrawToHospital(ally.Id);
        fight.Tick(1, DungeonRandom.FromSeed(2));
        var restored = RoundTrip(fight);
        Assert.Equal(Json(fight), Json(restored));
        Assert.True(restored.TimedOut);
        Assert.False(restored.OutcomeOf(restored.Hero(ally.Id)!).KnockedOut);
    }

    [Fact]
    public void Unsupported_or_invalid_checkpoint_cannot_silently_start_a_fresh_fight()
    {
        var fight = new DungeonSimulation(DungeonCatalog.Get(DungeonCatalog.DeadLands), []);
        var state = fight.Capture();
        Assert.Throws<InvalidOperationException>(() => DungeonSimulation.Restore(state with { Version = 2 }));
        Assert.Throws<InvalidOperationException>(() => DungeonSimulation.Restore(state with { Floor = -1 }));
        Assert.Throws<InvalidOperationException>(() => DungeonSimulation.Restore(state with { Foes = [state.Foes[0] with { Id = 42 }] }));
    }

    [Fact]
    public void Stateful_random_resumes_exactly_and_respects_full_integer_bounds()
    {
        var random = DungeonRandom.FromSeed(-41);
        for (var i = 0; i < 50; i++) random.NextDouble();
        var restored = new DungeonRandom(random.State);
        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(random.NextDouble(), restored.NextDouble());
            var number = random.Next(int.MinValue, int.MaxValue);
            Assert.InRange(number, int.MinValue, int.MaxValue - 1);
            Assert.Equal(number, restored.Next(int.MinValue, int.MaxValue));
        }
        Assert.Equal(3, random.Next(3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(4, 3));
    }
}
