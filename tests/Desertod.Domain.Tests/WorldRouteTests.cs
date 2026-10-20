using System.Security.Cryptography;
using System.Text.Json;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class WorldRouteTests
{
    [Fact]
    public void Districts_use_stable_ids_and_existing_generated_landmarks_without_touching_saved_world()
    {
        var v=NewVillage();v.Wilds=new(){CaveX=-20,CaveY=-24,LookoutX=-12,LookoutY=-28};
        var seen=Enumerable.Repeat(byte.MaxValue,Exploration.Size).ToArray();
        var before=JsonSerializer.Serialize(v);var hash=SHA256.HashData(seen);
        var routes=WorldRoutes.Discover(v,seen);
        Assert.Equal(new[]{"settlement","deer-woods","ridge-cave","lookout","gold-vein"},routes.Select(r=>r.Id));
        Assert.Equal(4,routes.Select(r=>r.District).Distinct().Count());
        Assert.Equal(v.Wilds.CaveMouth,routes.Single(r=>r.Id=="ridge-cave").Destination);
        Assert.Equal(v.Wilds.Lookout,routes.Single(r=>r.Id=="lookout").Destination);
        Assert.Equal(before,JsonSerializer.Serialize(v));Assert.Equal(hash,SHA256.HashData(seen));
        Assert.Equal(1350,Exploration.Size);
    }

    [Fact]
    public void Undiscovered_destinations_are_not_locatable_and_bridge_changes_do_not_erase_discovery()
    {
        var v=NewVillage();v.Wilds=new(){CaveX=-20,CaveY=-24,LookoutX=-12,LookoutY=-28};
        var initial=WorldRoutes.Discover(v,Exploration.Camp());
        Assert.Null(initial.Single(r=>r.Id=="ridge-cave").Destination);
        Assert.Equal("unexplored",initial.Single(r=>r.Id=="ridge-cave").BlockedBy);
        var seen=Enumerable.Repeat(byte.MaxValue,Exploration.Size).ToArray();
        Assert.Equal("bridge",WorldRoutes.Discover(v,seen).Single(r=>r.Id=="gold-vein").BlockedBy);
        v.BridgeOpened=true;
        Assert.Null(WorldRoutes.Discover(v,seen).Single(r=>r.Id=="gold-vein").BlockedBy);
        v.BridgeRuinedAt=Now;
        var cutOff=WorldRoutes.Discover(v,seen).Single(r=>r.Id=="gold-vein");
        Assert.Equal("bridge",cutOff.BlockedBy);Assert.NotNull(cutOff.Destination);
        Assert.Throws<Desertod.Domain.GameRuleException>(()=>BankAccess.Require(v,cutOff.Destination!.Value));
    }

    [Fact]
    public void Uninitialized_or_invalid_saved_landmarks_never_create_a_new_world_or_outside_destination()
    {
        var v=NewVillage();v.Wilds=null;Assert.Equal("unknown",WorldRoutes.Discover(v,Exploration.Camp()).Single(r=>r.Id=="lookout").BlockedBy);
        v.Wilds=new(){CaveX=int.MaxValue,CaveY=int.MinValue};
        var bad=WorldRoutes.Discover(v,Enumerable.Repeat(byte.MaxValue,Exploration.Size).ToArray()).Single(r=>r.Id=="ridge-cave");
        Assert.Null(bad.Destination);Assert.Equal("unexplored",bad.BlockedBy);
    }
}
