using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class MortalRoyalCombatApiTests(DesertodApiFactory factory)
{
    [Fact]
    public async Task Remedy_while_carried_saves_both_positions_and_stops_the_helper_once_after_reload()
    {
        var v=await players.FoundVillageAsync("Carried royal remedy");try
        {
            using var frozen=factory.Clock.Freeze();var helper=await players.JoinAsync(v,"Carrier");
            Desertod.Domain.World.TilePoint expected;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var c=await db.Citizens.SingleAsync(c=>c.Id==helper.Id);
                var now=factory.Clock.GetUtcNow();w.Structures.Add(new(){Kind=BuildingKind.Hospital,PlotId="20,15",Level=1,BuiltAt=now});
                w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));Assert.Equal(2,w.Store(ResourceKind.Remedy,2));
                w.TsarSpot=new(16.5,16.5);w.TsarWoundedAt=now;w.TsarInjuredUntil=now.AddMinutes(40);w.TsarRescueByCitizenId=c.Id;
                c.RescueJourney=RescueRules.PrepareRoute(w,new(15.5,13.5),w.TsarSpot.Value,w.OwnerAccountId,true,now,now);
                c.RescuePatientId=w.OwnerAccountId;var at=c.RescueJourney.PickupAt.AddTicks((c.RescueJourney.ArrivesAt-c.RescueJourney.PickupAt).Ticks/2);
                expected=c.RescueJourney.PositionAt(at);factory.Clock.Advance(at-now);await db.SaveChangesAsync();
            }
            var request=new RoyalHealRequest(Guid.NewGuid());
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/heal",request));
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/heal",request));
            await using var read=await Db();var city=await read.Villages.SingleAsync(w=>w.Slug==v.Slug);var carrier=await read.Citizens.SingleAsync(c=>c.Id==helper.Id);
            // PostgreSQL timestamps round100ns ticks to microseconds; compare physical position at that precision.
            Assert.True(city.TsarSpot!.Value.DistanceTo(expected)<.00001);Assert.Equal(city.TsarSpot,carrier.Spot);Assert.Null(carrier.RescuePatientId);
            Assert.NotNull(carrier.RescueJourney!.CompletedAt);Assert.False(carrier.RescueJourney.Delivered);
            Assert.Equal(0,carrier.Renown);Assert.Equal(1,city.Stock(ResourceKind.Remedy));Assert.Null(city.TsarInjuredUntil);
        }
        finally{await Cleanup(v);}
    }

    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task Cleanup(FoundedVillage v){await using var db=await Db();await db.Villages.Where(w=>w.Slug==v.Slug).ExecuteDeleteAsync();}
    private async Task<Guid> Announce(FoundedVillage v)
    {
        var state=await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Zombie",size="Small"}));
        return state.GetProperty("raids").EnumerateArray().Single(r=>r.GetProperty("outcome").GetString()=="Pending").GetProperty("id").GetGuid();
    }
    private static Task<HttpResponseMessage> Order(FoundedVillage v,Guid id)=>v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{id}/order",new{mode="hold"});

    [Fact]
    public async Task A_new_royal_body_reserves_finite_equipment_shoots_its_last_arrow_once_and_reloads_without_a_health_bonus()
    {
        var v=await players.FoundVillageAsync("Mortal archer");try
        {
            using var frozen=factory.Clock.Freeze();
            await players.ArrangeVillageAsync(v.Slug,(w,now)=>{
                w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));w.Store(ResourceKind.Bow,1);w.Store(ResourceKind.Arrows,1,sharpen:2);w.Store(ResourceKind.LeatherArmor,1);
            });
            var battle=await Announce(v);Assert.Equal(HttpStatusCode.OK,(await Order(v,battle)).StatusCode);
            Guid world;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);world=w.Id;var b=await db.RaidBattles.SingleAsync(b=>b.Id==battle);
                var f=Assert.Single(b.Fighters);Assert.Equal(1,b.CombatRulesVersion);Assert.Equal(1,f.CombatRulesVersion);
                Assert.Equal(32,f.Health);Assert.Equal(32,f.MaximumHealth);Assert.Equal(ResourceKind.Bow,w.RoyalCombat!.Weapon);
                Assert.Equal(1,w.RoyalCombat.Arrows);Assert.Equal(2,w.RoyalCombat.ArrowSharpen);
                Assert.Equal(0,w.Stock(ResourceKind.Bow));Assert.Equal(0,w.Stock(ResourceKind.Arrows));Assert.Equal(0,w.Stock(ResourceKind.LeatherArmor));
                var now=factory.Clock.GetUtcNow();var raid=w.Raids.Single(r=>r.Id==battle);raid.StrikesAt=now;
                b.StartsAt=now;b.LastTickAt=now;b.EndsAt=now.AddMinutes(5);b.Phase="Fighting";f.NextAttackAt=now;
                b.Units=[new(){X=f.X+.5,Y=f.Y,SpawnX=f.X+.5,SpawnY=f.Y,Health=100,MaximumHealth=100,Attack=0,Speed=0,NextAttackAt=now.AddHours(1)}];
                await db.SaveChangesAsync();
            }
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
            await factory.Services.GetRequiredService<GameService>().RaidVillageAsync(world,default);
            for(var i=0;i<2;i++)Assert.Equal(HttpStatusCode.OK,(await Order(v,battle)).StatusCode);
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Id==world);var f=Assert.Single((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);
                Assert.Equal(32,f.Health);Assert.Equal(1,f.ArrowsSpent);Assert.True(f.RangedDamage>0);Assert.Equal(0,w.RoyalCombat!.Arrows);Assert.Equal(0,w.RoyalCombat.ArrowSharpen);
                Assert.Equal(0,w.Stock(ResourceKind.Arrows));
            }
            var state=(await OkJsonAsync(await v.Tsar.GetAsync(v.Path))).GetProperty("state").GetProperty("tsar").GetProperty("combat");
            Assert.Equal(32,state.GetProperty("raidHealth").GetDouble());Assert.Equal(0,state.GetProperty("arrows").GetInt32());
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task A_legacy_announced_raid_keeps_80_health_and_does_not_borrow_new_equipment()
    {
        var v=await players.FoundVillageAsync("Legacy mortal baseline");try
        {
            Guid battle;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",factory.Clock.GetUtcNow()));Assert.Equal(1,w.Store(ResourceKind.Spear,1));
                var b=new RaidBattle{Id=Guid.CreateVersion7(),VillageId=w.Id,Sector="west",StartsAt=factory.Clock.GetUtcNow().AddHours(1),EndsAt=factory.Clock.GetUtcNow().AddHours(2)};
                db.RaidBattles.Add(b);await db.SaveChangesAsync();battle=b.Id;
            }
            Assert.Equal(HttpStatusCode.OK,(await Order(v,battle)).StatusCode);
            await using var read=await Db();var city=await read.Villages.SingleAsync(w=>w.Slug==v.Slug);var fighter=Assert.Single((await read.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);
            Assert.Equal(80,fighter.Health);Assert.Equal(0,fighter.CombatRulesVersion);Assert.Null(city.RoyalCombat);Assert.Equal(1,city.Stock(ResourceKind.Spear));
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task Concurrent_injury_healing_and_replay_spend_one_real_remedy_and_cannot_resurrect_a_fallen_fighter()
    {
        var v=await players.FoundVillageAsync("Finite royal remedy");try
        {
            var outsider=await players.JoinAsync(v);var battle=await Announce(v);await Order(v,battle);
            int stock;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",factory.Clock.GetUtcNow()));Assert.Equal(3,w.Store(ResourceKind.Remedy,3));stock=w.Stock(ResourceKind.Remedy);
                w.TsarInjuredUntil=factory.Clock.GetUtcNow().AddMinutes(10);w.TsarWoundedAt=factory.Clock.GetUtcNow();
                var f=Assert.Single((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);f.Health=0;f.Wounded=true;f.Active=false;
                await db.SaveChangesAsync();
            }
            var request=new RoyalHealRequest(Guid.NewGuid());
            await AssertForbiddenAsync(await outsider.Client.PostAsJsonAsync(v.Path+"/tsar/heal",request));
            var answers=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>v.Tsar.PostAsJsonAsync(v.Path+"/tsar/heal",request)));
            foreach(var answer in answers)await OkJsonAsync(answer);
            await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="hold"}),"defense.wounded");
            await using var read=await Db();var saved=await read.Villages.SingleAsync(w=>w.Slug==v.Slug);
            Assert.Null(saved.TsarInjuredUntil);Assert.Equal(stock-1,saved.Stock(ResourceKind.Remedy));
            Assert.Equal(1,await read.EconomyReceipts.CountAsync(r=>r.VillageId==saved.Id&&r.RequestId==request.RequestId));
            var fallen=Assert.Single((await read.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);Assert.Equal(0,fallen.Health);Assert.True(fallen.Wounded);
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task Finite_native_hospital_care_blocks_movement_only_until_recovery_and_keeps_political_actions_available()
    {
        var v=await players.FoundVillageAsync("Finite royal care");try
        {
            using var frozen=factory.Clock.Freeze();
            await players.ArrangeVillageAsync(v.Slug,(w,now)=>{w.TsarWoundedAt=now;w.TsarInjuredUntil=now.AddSeconds(10);w.TsarHospitalizedAt=now;});
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/goto",new{spot=new{x=20.5,y=13.5}}),"tsar.injured");
            await OkJsonAsync(await v.Tsar.GetAsync(v.Path+"/economy"));
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="enable-work-contracts"}));
            factory.Clock.Advance(TimeSpan.FromSeconds(11));
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/goto",new{spot=new{x=20.5,y=13.5}}));
            await using var read=await Db();var w=await read.Villages.SingleAsync(w=>w.Slug==v.Slug);
            Assert.Null(w.TsarInjuredUntil);Assert.Null(w.TsarHospitalizedAt);Assert.NotNull(w.TsarTrip);
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task Existing_resident_stats_are_the_rulers_actual_body_and_one_treatment_clears_the_shared_wound()
    {
        var v=await players.FoundVillageAsync("Resident royal stats");try
        {
            Guid citizen;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var c=CharacterCreation.Create(w.Id,w.OwnerAccountId,"Royal resident",factory.Clock.GetUtcNow());
                c.Stats.Endurance=5;c.Stats.Strength=7;db.Citizens.Add(c);await db.SaveChangesAsync();citizen=c.Id;
            }
            var battle=await Announce(v);Assert.Equal(HttpStatusCode.OK,(await Order(v,battle)).StatusCode);
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var c=await db.Citizens.SingleAsync(c=>c.Id==citizen);
                var f=Assert.Single((await db.RaidBattles.SingleAsync(b=>b.Id==battle)).Fighters);
                Assert.Equal(52,f.MaximumHealth);Assert.Equal(citizen,f.CitizenId);Assert.True(f.IsTsar);Assert.Null(w.RoyalCombat);
                var now=factory.Clock.GetUtcNow();CombatRules.Injure(c,TimeSpan.FromMinutes(10),now,true);w.TsarInjuredUntil=c.InjuredUntil;w.TsarWoundedAt=c.WoundedAt;
                w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));Assert.Equal(2,w.Store(ResourceKind.Remedy,2));f.Health=0;f.Wounded=true;f.Active=false;await db.SaveChangesAsync();
            }
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/heal",new RoyalHealRequest(Guid.NewGuid())));
            await using var read=await Db();var saved=await read.Villages.SingleAsync(w=>w.Slug==v.Slug);
            Assert.Null(saved.TsarInjuredUntil);Assert.Null((await read.Citizens.SingleAsync(c=>c.Id==citizen)).InjuredUntil);Assert.Null(saved.RoyalCombat);
        }
        finally{await Cleanup(v);}
    }
}
