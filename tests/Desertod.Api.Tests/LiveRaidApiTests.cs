using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class LiveRaidApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Fact]
    public async Task Announcement_persists_all_waves_and_the_countdown_has_no_early_movement()
    {
        var v=await players.FoundVillageAsync("Live warning");await players.JoinAsync(v);
        var json=await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Skeleton",size="Medium"}));
        var raids=json.GetProperty("raids").EnumerateArray().ToArray();
        await using var db=await Db();var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        var saved=await db.RaidBattles.AsNoTracking().Where(b=>b.VillageId==id).ToArrayAsync();
        Assert.Equal(2,saved.Length);
        foreach(var b in saved)
        {
            Assert.Equal("Warning",b.Phase);Assert.Equal(0,b.Revision);
            Assert.All(b.Units,u=>Assert.Equal(u.SpawnX,u.X));
            var raid=raids.Single(r=>r.GetProperty("id").GetGuid()==b.Id);
            Assert.Equal(2,raid.GetProperty("engineVersion").GetInt32());
            Assert.Equal(b.Units.Count,raid.GetProperty("count").GetInt32());
        }
        await factory.Services.GetRequiredService<GameService>().RaidVillageAsync(id,default);
        var state=await v.Tsar.GetFromJsonAsync<JsonElement>(v.Path);
        Assert.All(state.GetProperty("state").GetProperty("battles").EnumerateArray(),b=>Assert.Equal("Warning",b.GetProperty("phase").GetString()));
        await OkJsonAsync(await v.Tsar.DeleteAsync(v.Path+"/undead"));
        Assert.All(await db.RaidBattles.AsNoTracking().Where(b=>b.VillageId==id).ToArrayAsync(),b=>Assert.Equal("Cancelled",b.Phase));
    }
    [Fact]
    public async Task Target_clicks_cannot_add_attacks_consume_energy_or_control_another_village()
    {
        var v=await players.FoundVillageAsync("Battle orders");var worker=await players.JoinAsync(v);
        Guid battleId,target;
        await using(var db=await Db())
        {
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==worker.Id);var now=factory.Clock.GetUtcNow();
            citizen.CurrentTaskId=GameCatalog.Tasks.First(t=>t.Effect==TaskEffect.Guard).Id;
            citizen.CycleStartedAt=now;citizen.CycleEndsAt=now.AddHours(1);citizen.CycleSettled=false;
            var unit=new RaidUnit{X=-5.5,Y=13.5,SpawnX=-5.5,SpawnY=13.5,Health=20,MaximumHealth=20,Speed=.5,Attack=3,NextAttackAt=now.AddMinutes(5)};
            target=unit.Id;battleId=Guid.CreateVersion7();
            db.RaidBattles.Add(new(){Id=battleId,VillageId=citizen.VillageId,Sector="west",StartsAt=now.AddMinutes(5),LastTickAt=now.AddMinutes(5),EndsAt=now.AddMinutes(9),
                Units=[unit],Fighters=[new(){CitizenId=citizen.Id,Guard=true,Active=true,X=1.5,Y=13.5,Health=32,MaximumHealth=32,NextAttackAt=now.AddMinutes(5)}]});
            await db.SaveChangesAsync();
        }
        var before=await players.LoadCitizenAsync(worker.Id);
        var path=v.Path+$"/battles/{battleId}/order";
        for(var i=0;i<12;i++)Assert.Equal(HttpStatusCode.OK,(await worker.Client.PutAsJsonAsync(path,new{targetUnitId=target,mode="intercept"})).StatusCode);
        await using(var db=await Db())
        {
            var b=await db.RaidBattles.AsNoTracking().SingleAsync(b=>b.Id==battleId);
            Assert.Equal(20,b.Units.Single().Health);Assert.Equal(12,b.Revision);
            Assert.Equal(target,b.Fighters.Single().TargetUnitId);
        }
        Assert.Equal(before.StoredEnergy,(await players.LoadCitizenAsync(worker.Id)).StoredEnergy);
        await AssertRuleBrokenAsync(await worker.Client.PutAsJsonAsync(path,new{targetUnitId=Guid.NewGuid(),mode="intercept"}),"raid.targetGone");
        await players.ArrangeCitizenAsync(worker.Id,(c,now)=>c.InjuredUntil=now.AddMinutes(10));
        await AssertRuleBrokenAsync(await worker.Client.PutAsJsonAsync(path,new{targetUnitId=target,mode="hold"}),"citizen.injured");
        var other=await players.FoundVillageAsync("Another battle");var stranger=await players.JoinAsync(other);
        await players.ArrangeCitizenAsync(stranger.Id,(c,now)=>{c.CurrentTaskId=GameCatalog.Tasks.First(t=>t.Effect==TaskEffect.Guard).Id;c.CycleStartedAt=now;c.CycleEndsAt=now.AddHours(1);c.CycleSettled=false;});
        Assert.Equal(HttpStatusCode.BadRequest,(await stranger.Client.PutAsJsonAsync(other.Path+$"/battles/{battleId}/order",new{targetUnitId=target,mode="hold"})).StatusCode);
    }
    [Fact]
    public async Task Only_rulers_control_persisted_gates_and_breaches_stay_repairable()
    {
        var v=await players.FoundVillageAsync("Real gate");var worker=await players.JoinAsync(v);
        await using(var db=await Db())
        {
            var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
            db.VillageProjects.Add(new(){VillageId=id,DefinitionId=ProjectCatalog.Palisade,Status=ProjectStatus.Completed,StartedAt=factory.Clock.GetUtcNow()});
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await worker.Client.PutAsJsonAsync(v.Path+"/gate",new{open=false})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await v.Tsar.PutAsJsonAsync(v.Path+"/gate",new{open=false})).StatusCode);
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
            Assert.Equal(3,village.Fortifications.Count(f=>f.Gate));
            Assert.All(village.Fortifications.Where(f=>f.Gate),gate=>Assert.True(gate.Blocks));
        }
        await players.ArrangeCitizenAsync(worker.Id,(c,_)=>c.IsGovernor=true);
        Assert.Equal(HttpStatusCode.OK,(await worker.Client.PutAsJsonAsync(v.Path+"/gate",new{open=true})).StatusCode);
        await using(var db=await Db())Assert.All((await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug)).Fortifications.Where(f=>f.Gate),gate=>Assert.False(gate.Blocks));
    }
    [Fact]
    public async Task Legacy_pending_json_defaults_to_legacy_resolution_without_a_battle_row()
    {
        var v=await players.FoundVillageAsync("Legacy warning");await players.JoinAsync(v);
        await players.ArrangeVillageAsync(v.Slug,(village,now)=>{
            village.Raids.Add(new(){Enemy=EnemyKind.Zombie,Count=2,AnnouncedAt=now.AddMinutes(-4),StrikesAt=now.AddMinutes(-1)});
            RaidRules.UpdateNextRaidAt(village);
        });
        await using var db=await Db();var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        await factory.Services.GetRequiredService<GameService>().RaidVillageAsync(id,default);
        var saved=await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.NotNull(saved.Raids.Single().ResolvedAt);Assert.Empty(await db.RaidBattles.Where(b=>b.VillageId==id).ToArrayAsync());
    }
    [Fact]
    public async Task The_quoted_energy_is_debited_once_for_two_concurrent_start_requests()
    {
        var v=await players.FoundVillageAsync("Energy quote");var worker=await players.JoinAsync(v);
        await players.ArrangeCitizenAsync(worker.Id,(c,now)=>{c.StoredEnergy=20;c.EnergyUpdatedAt=now;});
        var quote=await worker.Client.GetFromJsonAsync<JsonElement>(v.Path+"/me");
        var before=await players.LoadCitizenAsync(worker.Id);
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>worker.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="forage"})));
        Assert.Single(results,r=>r.IsSuccessStatusCode);
        var after=await players.LoadCitizenAsync(worker.Id);
        var cost=quote.GetProperty("actionEnergyCosts").GetProperty("forage").GetDouble();
        Assert.InRange(before.StoredEnergy-after.StoredEnergy,cost-.02,cost+.02);
        Assert.False(after.CycleTired);
    }
    [Fact]
    public async Task Parallel_ticks_survive_reload_finish_once_and_trim_nested_legacy_history()
    {
        var v=await players.FoundVillageAsync("Persisted fight");var worker=await players.JoinAsync(v);
        await players.ArrangeCitizenAsync(worker.Id,(c,now)=>{c.CurrentTaskId=GameCatalog.KeepWatch;c.CycleStartedAt=now;c.CycleEndsAt=now.AddHours(1);c.CycleSettled=false;});
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{
            for(var i=0;i<11;i++)city.Raids.Add(new(){Enemy=EnemyKind.Skeleton,Count=1,Outcome=RaidOutcome.Repelled,StrikesAt=now.AddMinutes(-20-i),ResolvedAt=now.AddMinutes(-19-i),Loot=[new(){Kind=ResourceKind.IronOre,Amount=1}]});
        });
        var announced=await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Skeleton",size="Small"}));
        var raidId=announced.GetProperty("raids").EnumerateArray().Single(r=>r.GetProperty("outcome").GetString()=="Pending").GetProperty("id").GetGuid();
        Guid villageId;
        await using(var db=await Db())
        {
            villageId=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
            // Preparation is a real writer too. Freeze its row snapshot with the same
            // database lock as the worker; the parallel tick assertions below stay concurrent.
            await using var transaction=await db.Database.BeginTransactionAsync();
            var lockKey=villageId.ToString();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
            var city=await db.Villages.SingleAsync(x=>x.Id==villageId);
            var battle=await db.RaidBattles.SingleAsync(b=>b.Id==raidId);var now=factory.Clock.GetUtcNow();
            battle.StartsAt=now.AddSeconds(-1);battle.LastTickAt=battle.StartsAt;battle.EndsAt=now.AddMinutes(4);
            city.Raids.Single(r=>r.Id==raidId).StrikesAt=battle.StartsAt;RaidRules.UpdateNextRaidAt(city);
            // The wall is completed during preparation, after composition has already been frozen.
            db.VillageProjects.Add(new(){VillageId=city.Id,DefinitionId=ProjectCatalog.Palisade,Status=ProjectStatus.Completed,StartedAt=now});
            // This regression verifies transactions/rewards, not randomized combat balance.
            // Every prepared variant needs one real guard hit, keeping the time bound stable.
            foreach(var unit in battle.Units){unit.X=14.5;unit.Y=13.5;unit.Health=unit.MaximumHealth=1;unit.Speed=0;unit.Attack=0;}
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var service=factory.Services.GetRequiredService<GameService>();
        await service.RaidVillageAsync(villageId,default);
        await using(var reloaded=await Db())
        {
            Assert.True((await reloaded.RaidBattles.AsNoTracking().SingleAsync(b=>b.Id==raidId)).Revision>0);
            Assert.NotEmpty((await reloaded.Villages.AsNoTracking().SingleAsync(c=>c.Id==villageId)).Fortifications);
        }
        for(var i=0;i<12;i++)
        {
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.WhenAll(service.RaidVillageAsync(villageId,default),service.RaidVillageAsync(villageId,default));
        }
        await using(var db=await Db())
        {
            var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==villageId);
            var battle=await db.RaidBattles.AsNoTracking().SingleAsync(b=>b.Id==raidId);
            Assert.Equal("Finished",battle.Phase);Assert.NotNull(battle.FinishedAt);
            Assert.InRange(city.Raids.Count,1,RaidRules.KeptRaids);
            Assert.Equal(battle.Units.Count,city.Raids.Single(r=>r.Id==raidId).Killed);
            Assert.Equal(battle.Units.Count*RaidRules.RenownPerKill,(await players.LoadCitizenAsync(worker.Id)).Renown);
        }
        var renown=(await players.LoadCitizenAsync(worker.Id)).Renown;
        await service.RaidVillageAsync(villageId,default);Assert.Equal(renown,(await players.LoadCitizenAsync(worker.Id)).Renown);
    }
    [Fact]
    public async Task Three_overdue_waves_stay_serial_when_the_second_start_moves_past_the_third_schedule()
    {
        var v=await players.FoundVillageAsync("Serial waves");var worker=await players.JoinAsync(v);
        await players.ArrangeCitizenAsync(worker.Id,(c,now)=>{c.CurrentTaskId=GameCatalog.KeepWatch;c.CycleStartedAt=now.AddHours(-1);c.CycleEndsAt=now.AddHours(1);c.CycleSettled=false;});
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Skeleton",size="Large"}));
        var villageId=(await players.LoadCitizenAsync(worker.Id)).VillageId;
        Guid[] waveIds;DateTimeOffset thirdSchedule;double thirdX;
        // Fixture edits share the production database lock with real background workers.
        await using(var db=await Db())
        {
            await using var transaction=await db.Database.BeginTransactionAsync();
            var lockKey=villageId.ToString();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
            var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
            var raids=city.Raids.OrderBy(r=>r.Wave).ToArray();waveIds=raids.Select(r=>r.Id).ToArray();
            Assert.Equal(3,raids.Length);
            for(var i=0;i<raids.Length;i++)
            {
                var battle=await db.RaidBattles.SingleAsync(b=>b.Id==raids[i].Id);
                battle.ScheduledAt=battle.StartsAt=raids[i].StrikesAt=factory.Clock.GetUtcNow().AddMinutes(-4+i);
                battle.LastTickAt=i==0?factory.Clock.GetUtcNow().AddSeconds(-1):battle.StartsAt;
                battle.EndsAt=factory.Clock.GetUtcNow().AddMinutes(4);
                foreach(var unit in battle.Units){unit.Attack=0;if(i==0){unit.Health=0;unit.State="Dead";}}
                if(i==0)battle.Fighters.Add(new(){CitizenId=worker.Id,X=12.5,Y=13.5,Health=17,MaximumHealth=44,Active=true,Guard=true});
            }
            RaidRules.UpdateNextRaidAt(city);await db.SaveChangesAsync();
            var third=await db.RaidBattles.SingleAsync(b=>b.Id==waveIds[2]);await db.Entry(third).ReloadAsync();thirdSchedule=third.ScheduledAt;thirdX=third.Units[0].X;
            await transaction.CommitAsync();
        }
        var service=factory.Services.GetRequiredService<GameService>();
        await service.RaidVillageAsync(villageId,default); // Finish wave one; reserve both waiting waves.
        await service.RaidVillageAsync(villageId,default); // Start two later than three's original deadline.
        for(var i=0;i<3;i++){factory.Clock.Advance(TimeSpan.FromSeconds(1));await service.RaidVillageAsync(villageId,default);}
        await using(var db=await Db())
        {
            var second=await db.RaidBattles.AsNoTracking().SingleAsync(b=>b.Id==waveIds[1]);
            var third=await db.RaidBattles.AsNoTracking().SingleAsync(b=>b.Id==waveIds[2]);
            Assert.NotEqual("Warning",second.Phase);Assert.Null(second.FinishedAt);
            Assert.True(second.StartsAt>third.ScheduledAt);
            Assert.Equal(17,second.Fighters.Single(f=>f.CitizenId==worker.Id).Health);
            Assert.Equal("Warning",third.Phase);Assert.True(third.Deferred);Assert.Null(third.FinishedAt);
            Assert.Equal(thirdSchedule,third.ScheduledAt);Assert.Equal(thirdX,third.Units[0].X);
            Assert.All(third.Units,u=>Assert.Equal(u.MaximumHealth,u.Health));
        }
    }
    [Fact]
    public void Notification_batches_publish_only_committed_effects()
    {
        var changes=new VillageStateChanges();var count=0;
        using(var discarded=changes.BeginBatch())Assert.True(changes.Defer(()=>count++));
        Assert.Equal(0,count);
        using(var committed=changes.BeginBatch()){Assert.True(changes.Defer(()=>count++));committed.Commit();}
        Assert.Equal(1,count);Assert.False(changes.Defer(()=>count++));
    }
}
