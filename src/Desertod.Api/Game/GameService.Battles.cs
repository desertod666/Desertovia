using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Desertod.Domain.World;
using Desertod.Domain.Dungeons;

namespace Desertod.Api.Game;

public sealed record BattleOrder(Guid? TargetUnitId = null, string Mode = "intercept", TilePoint? Destination = null, Guid? RequestId = null);
public sealed record GateOrder(bool Open);

public sealed partial class GameService
{
    private async Task PrepareBattleAsync(DesertodDbContext db,Village village,Raid raid,DateTimeOffset now,CancellationToken ct,string? sector=null,string? composition=null)
    {
        if(village.DefeatedAt is not null)throw new GameRuleException("village.defeated","Restore the royal castle before starting another raid.");
        var people=await db.Citizens.Where(c=>c.VillageId==village.Id&&c.Status==CitizenStatus.Active).ToListAsync(ct);
        var completed=await db.VillageProjects.Where(p=>p.VillageId==village.Id&&p.Status==ProjectStatus.Completed)
            .Select(p=>p.DefinitionId).ToListAsync(ct);
        db.RaidBattles.Add(RaidBalance.Prepare(village,raid,people,completed,
            people.Where(c=>presence.IsPresent(c.Id)).Select(c=>c.Id).ToHashSet(),now,sector,composition));
    }

    private static void CarryFighters(RaidBattle previous,RaidBattle next,DateTimeOffset now)
    {
        foreach(var f in previous.Fighters)
        {
            // A queued order must never create a second actor or restore spent health.
            next.Fighters.RemoveAll(x=>x.IsTsar==f.IsTsar&&x.ActorId==f.ActorId);
            next.Fighters.Add(new(){CitizenId=f.CitizenId,AccountId=f.AccountId,IsTsar=f.IsTsar,X=f.X,Y=f.Y,Health=f.Health,MaximumHealth=f.MaximumHealth,
                Mode=f.Mode,Guard=f.Guard,Active=f.Active,Wounded=f.Wounded,HealReadyAt=f.HealReadyAt,HealingReceipts=f.HealingReceipts,NextAttackAt=now.AddSeconds(1)});
        }
    }

    private static RaidReport BattleReport(Raid raid,RaidBattle battle,IReadOnlyList<Citizen> people) => new(
        raid.Id,raid.Source,raid.Enemy,raid.Count,raid.Wave,raid.Waves,raid.StrikesAt,raid.Outcome==RaidOutcome.Repelled,
        raid.Killed,0,0,
        battle.Fighters.Where(f=>!f.IsTsar&&f.DamageDealt>0).Select(f=>new RaidDefenderReport(f.CitizenId,
            people.FirstOrDefault(c=>c.Id==f.CitizenId)?.Name??"",f.Wounded,f.RenownEarned,BattleBroken(f,people))).ToArray(),
        battle.Fighters.Where(f=>!f.IsTsar&&f.Wounded&&f.DamageDealt==0).Select(f=>new RaidVictimReport(f.CitizenId,
            people.FirstOrDefault(c=>c.Id==f.CitizenId)?.Name??"")).ToArray(),
        raid.Loot.Select(g=>new Desertod.Domain.Catalog.ResourceAmount(g.Kind,g.Amount)).ToArray(),
        raid.Stolen.Select(g=>new Desertod.Domain.Catalog.ResourceAmount(g.Kind,g.Amount)).ToArray(),
        battle.Damage.Where(d=>d.Amount>0).Select(d=>d.PlotId).ToArray(),raid.Outcome==RaidOutcome.Repelled?RaidRules.RepelledMorale:raid.Outcome==RaidOutcome.Lost?RaidRules.LostMorale:0) {EngineVersion=2,Summary=raid.Summary,Outcome=raid.Outcome};

    private static IReadOnlyList<Desertod.Domain.Catalog.ResourceKind> BattleBroken(RaidFighter fighter,IReadOnlyList<Citizen> people)
    {
        var c=people.FirstOrDefault(c=>c.Id==fighter.CitizenId);
        return new[]{fighter.Weapon!=c?.Weapon?fighter.Weapon:null,fighter.Armor!=c?.Armor?fighter.Armor:null}
            .Where(kind=>kind is not null).Select(kind=>kind!.Value).ToArray();
    }

    public async Task SetBattleOrderAsync(string slug,Guid actor,Guid battleId,BattleOrder order,CancellationToken ct)
    {
        if(order.Mode is not ("hold" or "intercept" or "retreat" or "heal"))throw new GameRuleException("defense.mode","Unknown defense mode.");
        var villageId=await FindVillageIdAsync(slug,ct);
        await WithVillageAsync(villageId,async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);
            var citizen=await db.Citizens.SingleOrDefaultAsync(c=>c.VillageId==villageId&&c.AccountId==actor,ct);
            var tsar=village.OwnerAccountId==actor;
            if(!tsar&&citizen is null)throw new UnauthorizedAccessException();
            var restState = tsar ? village.TsarRestState : citizen!.RestState;
            if (restState == RestState.Resting || restState == RestState.RestRequested && order.Mode is not ("retreat" or "heal"))
                SafeRest.RequireActive(restState);
            var now=time.GetUtcNow();
            await SyncHospitalAsync(db,villageId,ct,true,actor,actorOnly:true);
            if(HasHospitalRestriction(actor))throw new GameRuleException("hospital.unavailable","Wait until recovery.");
            if(citizen is not null)HospitalRules.RequireReady(citizen,now);
            var battle=await db.RaidBattles.SingleOrDefaultAsync(b=>b.Id==battleId&&b.VillageId==villageId&&b.FinishedAt==null,ct)
                ??throw new GameRuleException("raid.finished","This battle has finished.");
            var queue=await db.RaidBattles.Where(b=>b.VillageId==villageId&&b.FinishedAt==null).ToListAsync(ct);
            if(battle.Phase=="Warning"&&queue.Any(b=>b.Id!=battle.Id&&(b.Phase!="Warning"||b.ScheduledAt<battle.ScheduledAt)))
                throw new GameRuleException("raid.queued","Wait for the current wave before issuing orders to the next one.");
            if(order.TargetUnitId is {} target && !battle.Units.Any(u=>u.Id==target&&u.Alive))
                throw new GameRuleException("raid.targetGone","This enemy is no longer available.");
            if(order.Mode is not ("heal" or "retreat")&&queue.Any(b=>b.Id!=battle.Id&&b.Fighters.Any(f=>f.Active&&f.Health>0&&IsActor(f,actor,citizen?.Id))))
                throw new GameRuleException("raid.queued","Retreat from the current battle before joining another one.");
            if(order.Mode!="retreat")await RequireNoDungeonActorAsync(db,villageId,actor,ct);
            var ownFighter=battle.Fighters.Where(f=>IsActor(f,actor,citizen?.Id))
                .OrderByDescending(f=>f.Active&&f.Health>0&&!f.Wounded&&f.Mode!="withdrawn").ThenByDescending(f=>f.IsTsar).FirstOrDefault();
            if(order.Mode is "heal" or "retreat")
            {
                if(battle.Phase=="Warning")throw new GameRuleException("battle.notActive","The battle has not started.");
                var own=ownFighter
                    ??throw new GameRuleException("battle.notActive","You are not actively fighting.");
                if(order.Mode=="heal") { if(BattleActions.Heal(village,own.IsTsar?null:citizen,own,order.RequestId??Guid.Empty,now))battle.RemediesUsed++; }
                else BattleActions.Retreat(battle,own,new RaidNavigation(village));
                battle.Revision++;db.AuditActor=actor;db.AuditOperation="battle-"+order.Mode;
                await db.SaveChangesAsync(ct);changes.Mark(villageId);
                await notifier.BattleUpdated(slug,battle,village.Fortifications);
                if(citizen is not null)await notifier.CitizenUpdated(slug,citizen.ToSummaryDto(now));
                return true;
            }
            var nav=new RaidNavigation(village);
            if(order.Destination is {} destination && (!double.IsFinite(destination.X)||!double.IsFinite(destination.Y)
                ||!nav.Pass((int)Math.Floor(destination.X),(int)Math.Floor(destination.Y))))
                throw new GameRuleException("walk.blocked","Choose reachable ground.");
            RaidFighter? fighter;
            if(tsar)
            {
                if(village.TsarInjuredUntil>now)throw new GameRuleException("defense.wounded","The Tsar is recovering.");
                if(citizen is not null && (citizen.DungeonRunId is not null || CombatRules.IsInjured(citizen,now)
                    ||citizen.ExhaustedUntil>now || !citizen.CycleSettled&&ownFighter is null))
                    throw new GameRuleException("defense.busy","Finish the current work or recovery first.");
                fighter=ownFighter;
                if(fighter is null)
                {
                    var p=nav.NearestFree(village.TsarTrip?.PositionAt(now)??village.TsarSpot??WorldMap.Places[WorldMap.Camp]);
                    fighter=new(){IsTsar=true,AccountId=actor,X=p.X,Y=p.Y,Health=HeroClassCatalog.VoivodeHealth,
                        MaximumHealth=HeroClassCatalog.VoivodeHealth,Guard=false,Active=true,NextAttackAt=now.AddSeconds(1)};
                    battle.Fighters.Add(fighter);village.TsarTrip=null;village.TsarSpot=p;
                }
            }
            else
            {
                if(CombatRules.IsInjured(citizen!,now))throw new GameRuleException("citizen.injured","A wounded citizen cannot attack.");
                if(!RaidSimulation.CanFight(citizen!,now))throw new GameRuleException("battle.notReady","Wait until recovery or return from the dungeon.");
                fighter=battle.Fighters.SingleOrDefault(f=>!f.IsTsar&&f.CitizenId==citizen!.Id);
                if(fighter is null)
                {
                    if(citizen!.Weapon is null)CombatRules.Arm(citizen,village);
                    var p=nav.NearestFree(citizen!.Trip?.PositionAt(now)??citizen.Spot??citizen.IdleSpot??WorldMap.Places[WorldMap.Camp]);
                    fighter=new(){CitizenId=citizen.Id,X=p.X,Y=p.Y,Health=32+citizen.Stats.Endurance*4,
                        MaximumHealth=32+citizen.Stats.Endurance*4,Guard=false,Active=true,NextAttackAt=now.AddSeconds(1)};
                    battle.Fighters.Add(fighter);
                }
            }
            if(fighter.Health<=0)throw new GameRuleException("defense.wounded","Wait until this encounter is over and recover.");
            fighter.Active=true;fighter.Mode=order.Mode;fighter.TargetUnitId=order.Destination is null?order.TargetUnitId:null;
            fighter.DestinationX=order.Destination?.X;fighter.DestinationY=order.Destination?.Y;battle.Revision++;
            db.AuditActor=actor;db.AuditOperation="battle-order";
            await db.SaveChangesAsync(ct);changes.Mark(villageId);
            await notifier.BattleUpdated(slug,battle,village.Fortifications);return true;
        },ct);
    }

    public async Task<VillageStateDto> UpgradeFortificationsAsync(string slug,Guid actor,CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);await RequireRulerAsync(db,village,actor,ct);
            var completed=await db.VillageProjects.Where(p=>p.VillageId==village.Id&&p.Status==ProjectStatus.Completed).Select(p=>p.DefinitionId).ToListAsync(ct);
            Fortifications.Ensure(village,completed);
            var now=time.GetUtcNow();var order=FortificationUpgrades.Request(village,actor,now);
            if(village.OwnerAccountId==actor)FortificationUpgrades.Begin(village,order,now);
            db.AuditActor=actor;db.AuditOperation="fortification-upgrade";
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            return await BuildStateAsync(db,village,now,ct);
        },ct);

    public async Task SetGateAsync(string slug,Guid actor,GateOrder order,CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);
            await RequireRulerAsync(db,village,actor,ct);
            var completed=await db.VillageProjects.Where(p=>p.VillageId==village.Id&&p.Status==ProjectStatus.Completed).Select(p=>p.DefinitionId).ToListAsync(ct);
            Fortifications.Ensure(village,completed);
            if(village.Fortifications.Count==0)throw new GameRuleException("defense.noWall","Build the palisade first.");
            foreach(var gate in village.Fortifications.Where(f=>f.Gate))gate.Open=order.Open;
            db.AuditActor=actor;db.AuditOperation="gate";
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);return true;
        },ct);
}
