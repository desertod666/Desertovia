using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class RoyalDungeonApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private GameService Game=>factory.Services.GetRequiredService<GameService>();
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task Cleanup(FoundedVillage v){await using var db=await Db();await db.Villages.Where(w=>w.Slug==v.Slug).ExecuteDeleteAsync();}
    private async Task<Guid> Open(FoundedVillage v)
    {
        var state=await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/dungeon",new{dungeonId="dead-lands"}));
        Assert.Equal(1,state.GetProperty("dungeon").GetProperty("combatRulesVersion").GetInt32());
        return state.GetProperty("dungeon").GetProperty("id").GetGuid();
    }
    private async Task<DungeonStart> Enter(Guid id)
    {
        var configuration=factory.Services.GetRequiredService<IConfiguration>();
        var previous=configuration["Game:DevTools"];configuration["Game:DevTools"]="false";
        try
        {
            await using(var db=await Db()){var run=await db.DungeonRuns.SingleAsync(r=>r.Id==id);factory.Clock.Advance(run.GatheringEndsAt-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));}
            await Game.AdvanceDungeonsAsync(default);
            await using(var db=await Db()){var run=await db.DungeonRuns.SingleAsync(r=>r.Id==id);factory.Clock.Advance(run.MarchEndsAt!.Value-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));}
            await Game.AdvanceDungeonsAsync(default);
            var runtime=factory.Services.GetRequiredService<DungeonRuntime>();
            if(runtime.Find(id) is {} live){await live.Operation.WaitAsync();try{runtime.Remove(id);}finally{live.Operation.Release();}}
            var restored=Assert.Single(await Game.RestoreDungeonsAsync(default),r=>r.RunId==id);
            Assert.All(restored.Fighters,f=>Assert.False(f.Invulnerable));
            return restored;
        }
        finally{configuration["Game:DevTools"]=previous;}
    }

    [Fact]
    public async Task Ordinary_royal_classes_reserve_real_arrows_restore_the_frozen_class_and_settle_one_training_result()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Royal archer dungeon");try
        {
            await players.ArrangeVillageAsync(v.Slug,(w,now)=>{
                w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));Assert.Equal(1,w.Store(ResourceKind.Bow,1));Assert.Equal(1,w.Store(ResourceKind.Arrows,1,sharpen:2));
                w.RoyalCombat=new(){Stats=new(){Dexterity=3},Skills=[new(){Kind=Skill.Hunting,Level=2}]};
            });
            var id=await Open(v);var outsider=await players.JoinAsync(v,"Other archer");
            await AssertForbiddenAsync(await outsider.Client.GetAsync(v.Path+"/tsar/dungeon"));
            await AssertForbiddenAsync(await outsider.Client.PostAsJsonAsync(v.Path+"/tsar/dungeon",new{@class="Archer"}));
            var offered=await OkJsonAsync(await v.Tsar.GetAsync(v.Path+"/tsar/dungeon"));Assert.Contains(offered.EnumerateArray(),o=>o.GetProperty("class").GetString()=="Archer");
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/dungeon",new{@class="Archer"}));
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/dungeon",new{@class="Archer"}));
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);Assert.Equal(0,w.Stock(ResourceKind.Bow));Assert.Equal(0,w.Stock(ResourceKind.Arrows));Assert.Equal(1,w.RoyalCombat!.Arrows);
            }
            var start=await Enter(id);var fighter=Assert.Single(start.Fighters);
            Assert.Equal(HeroClass.Voivode,fighter.Class);Assert.Equal(HeroClass.Archer,fighter.CombatClass);Assert.Equal(18,fighter.Health);Assert.Equal(1,fighter.Arrows);
            Assert.Equal(HeroClassCatalog.Get(HeroClass.Archer),fighter.CombatDefinition);
            var restored=start.Save!.Resume((await Load(id)),v.Slug);Assert.Equal(start.Save.ToJson(),restored.Save!.ToJson());
            var view=Assert.IsType<DungeonRunDto>(await Game.GetDungeonRunAsync(id,fighter.AccountId,default));Assert.Equal(HeroClass.Archer,Assert.Single(view.Heroes).CombatClass);
            var result=new DungeonHeroOutcome(1,10,[],0,0,false,false,null);
            await Game.SettleDungeonAsync(id,start.VillageId,[new(fighter.Id,result)],DungeonOutcome.Defeat,1,default);
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);Assert.Equal(1,w.Stock(ResourceKind.Bow));Assert.Equal(0,w.Stock(ResourceKind.Arrows));Assert.Equal(0,w.RoyalCombat!.Arrows);
                Assert.True(w.RoyalCombat.Stats.DexterityExperience>0);
            }
            var before=(await Load(id)).Heroes[0].Experience;
            await Game.SettleDungeonAsync(id,start.VillageId,[new(fighter.Id,result)],DungeonOutcome.Defeat,1,default);Assert.Equal(before,(await Load(id)).Heroes[0].Experience);
        }
        finally{await Cleanup(v);}
    }

    private async Task<DungeonRun> Load(Guid id){await using var db=await Db();return await db.DungeonRuns.AsNoTracking().SingleAsync(r=>r.Id==id);}

    [Fact]
    public async Task A_new_call_with_a_missing_checkpoint_is_retained_for_repair_without_abandoning_or_renewing_the_body()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Retain accepted body");try
        {
            var id=await Open(v);
            await using(var db=await Db()){var run=await db.DungeonRuns.SingleAsync(r=>r.Id==id);run.Phase=DungeonPhase.Running;run.CheckpointJson=null;await db.SaveChangesAsync();}
            await Game.AbandonDungeonsAsync(default);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Game.GetDungeonRunAsync(id,null,default));
            var saved=await Load(id);Assert.Equal(DungeonPhase.Running,saved.Phase);Assert.Null(saved.CheckpointJson);Assert.False(saved.Tsar.Settled);
            await using var check=await Db();Assert.Equal(0,(await check.Villages.SingleAsync(w=>w.Slug==v.Slug)).RoyalCombat!.Stats.StrengthExperience);
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task A_resident_is_the_frozen_royal_body_and_its_knockout_and_treatment_have_one_shared_wound()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Royal resident dungeon");try
        {
            Guid citizen;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var c=CharacterCreation.Create(w.Id,w.OwnerAccountId,"Actual body",factory.Clock.GetUtcNow());c.Stats.Endurance=5;c.Stats.Strength=7;db.Citizens.Add(c);
                w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",factory.Clock.GetUtcNow()));Assert.Equal(1,w.Store(ResourceKind.Remedy,1));await db.SaveChangesAsync();citizen=c.Id;
            }
            var id=await Open(v);Assert.Equal(citizen,(await Load(id)).Tsar.RoyalBodyCitizenId);
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/me/dungeon",new{@class="Brawler"}),"dungeon.busy");
            var start=await Enter(id);var fighter=Assert.Single(start.Fighters);Assert.Equal(40,fighter.Health);
            var result=new DungeonHeroOutcome(0,10,[],0,0,false,true,EnemyKind.Zombie);
            await Game.SettleDungeonAsync(id,start.VillageId,[new(fighter.Id,result)],DungeonOutcome.Defeat,0,default);
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var c=await db.Citizens.SingleAsync(c=>c.Id==citizen);
                Assert.Null(w.RoyalCombat);Assert.Equal(w.TsarInjuredUntil,c.InjuredUntil);Assert.Equal(w.TsarWoundedAt,c.WoundedAt);Assert.True(c.InjuredUntil>factory.Clock.GetUtcNow());
                Assert.False(c.RescueRewardEligible);Assert.True(w.TsarRescueRewardEligible);Assert.Null(c.DungeonRunId);Assert.Null(c.Trip);Assert.Null(w.TsarTrip);Assert.Equal(w.TsarSpot,c.Spot);
                Assert.True(c.Stats.StrengthExperience>0);
            }
            await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/heal",new RoyalHealRequest(Guid.NewGuid())));
            await using var check=await Db();var healed=await check.Citizens.SingleAsync(c=>c.Id==citizen);Assert.Null(healed.InjuredUntil);Assert.Null((await check.Villages.SingleAsync(w=>w.Slug==v.Slug)).TsarInjuredUntil);
        }
        finally{await Cleanup(v);}
    }

    [Fact]
    public async Task Cancelling_returns_all_reserved_royal_gear_and_a_legacy_call_keeps_its_80_health_contract()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Royal class cancellation");try
        {
            await players.ArrangeVillageAsync(v.Slug,(w,now)=>{w.Storages.Add(StorageBuilding.Create(StorageKind.Heap,"17,12",now));Assert.Equal(1,w.Store(ResourceKind.Spear,1));Assert.Equal(1,w.Store(ResourceKind.LeatherArmor,1));});
            await Open(v);await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/dungeon",new{@class="Spearman"}));
            await OkJsonAsync(await v.Tsar.DeleteAsync(v.Path+"/dungeon"));
            Guid id;
            await using(var db=await Db())
            {
                var w=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);Assert.Equal(1,w.Stock(ResourceKind.Spear));Assert.Equal(1,w.Stock(ResourceKind.LeatherArmor));Assert.Null(w.RoyalCombat!.Weapon);Assert.Null(w.RoyalCombat.Armor);
                var now=factory.Clock.GetUtcNow();var old=new DungeonRun{VillageId=w.Id,DungeonId=DungeonCatalog.DeadLands,Phase=DungeonPhase.Gathering,OpenedAt=now,GatheringEndsAt=now.AddMinutes(1),
                    Heroes=[new(){AccountId=w.OwnerAccountId,Name="Legacy ruler",Class=HeroClass.Voivode}]};db.DungeonRuns.Add(old);await db.SaveChangesAsync();id=old.Id;
            }
            await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/tsar/dungeon",new{@class="Spearman"}),"dungeon.classUnavailable");
            var start=await Enter(id);var oldFighter=Assert.Single(start.Fighters);Assert.Equal(80,oldFighter.Health);Assert.Equal(8,oldFighter.Damage);Assert.Null(oldFighter.CombatClass);
            await using var read=await Db();Assert.Equal(1,(await read.Villages.SingleAsync(w=>w.Slug==v.Slug)).Stock(ResourceKind.Spear));
        }
        finally{await Cleanup(v);}
    }
}
