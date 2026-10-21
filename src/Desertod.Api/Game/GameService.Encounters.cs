using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    private static async Task<bool> HasPersonalEncounterAsync(DesertodDbContext db,Guid villageId,Guid accountId,CancellationToken ct)
    {
        var runs=await db.DungeonRuns.Where(r=>r.VillageId==villageId&&r.Kind==DungeonRunKind.Ambush&&r.Phase!=DungeonPhase.Ended).ToListAsync(ct);
        return runs.Any(r=>r.Heroes.Any(h=>h.AccountId==accountId&&!h.Settled));
    }

    private async Task<bool> PrepareWorkEncounterAsync(DesertodDbContext db,Citizen citizen,WorkContext context,DateTimeOffset now,CancellationToken ct)
    {
        if(citizen.CycleEncounterVersion!=1)return true;
        context.SkipAmbush=true;
        if(citizen.DungeonRunId is not null)return false;
        if(citizen.CycleAmbushChecked)
        {
            context.ResolvedAmbush=WorkEncounterRules.Report(citizen);
            if(citizen.CycleEncounterRunId is not null&&context.ResolvedAmbush is null)throw new GameRuleException("ambush.changed","The saved encounter result is missing.");
            return true;
        }
        if(citizen.CycleSettled||citizen.CycleEndsAt is null||citizen.CycleEndsAt>now||citizen.RecoveryPausedAt is not null
            ||citizen.Trip?.Waiting(now)==true||HospitalRules.IsAdmitted(citizen,now))return true;
        if(CombatRules.IsInjured(citizen,now))return false;
        if(GameCatalog.FindTask(citizen.CurrentTaskId??"")?.Danger is not {} danger)return true;
        var raids=await db.RaidBattles.Where(b=>b.VillageId==citizen.VillageId&&b.FinishedAt==null).ToListAsync(ct);
        if(raids.Any(b=>b.Fighters.Any(f=>f.Active&&f.Health>0&&(!f.IsTsar&&f.CitizenId==citizen.Id||f.IsTsar&&f.AccountId==citizen.AccountId))))return false;
        var random=DungeonRandom.FromSeed(citizen.CycleEncounterSeed);
        var enemy=CombatRules.SelectAmbush(context.Village,danger,random,citizen.CycleStartedAt??now);
        citizen.CycleAmbushChecked=true;
        if(enemy is null)return true;
        var run=WorkEncounterRules.Open(citizen,enemy.Kind,citizen.CycleEncounterSeed,now);
        var fighter=AmbushScene.Fighter(citizen);
        var simulation=new DungeonSimulation(AmbushScene.Map(enemy.Kind),[fighter]);
        var heroes=new[]{new DungeonRunHeroDto(citizen.Id,citizen.Id,citizen.Name,fighter.Class,citizen.Appearance,fighter.Health)};
        run.CheckpointJson=new DungeonSave(0,simulation.Capture(),random.State,heroes,[]).ToJson();
        db.DungeonRuns.Add(run);await db.SaveChangesAsync(ct);changes.Mark(citizen.VillageId);
        await notifier.CitizenUpdated(context.Village.Slug,citizen.ToSummaryDto(now));
        return false;
    }

    private async Task SettleWorkEncounterAsync(DesertodDbContext db,Village village,DungeonRun run,DungeonHero hero,Citizen citizen,DungeonHeroOutcome outcome,Random random,DateTimeOffset now,CancellationToken ct)
    {
        if(hero.Settled)return;
        WorkEncounterRules.Settle(run,hero,citizen,outcome,random,now);
        var context=(await LoadContextAsync(db,village,citizen.Id,now,ct)).Work;
        var report=await SettleWorkAsync(db,citizen,context,now,ct);
        await AfterSettleAsync(db,village,report,now,ct);
        var completion=await CompleteProjectIfDoneAsync(db,context,now,ct);
        var quests=await UpdateQuestsAsync(db,village,citizen,report,now,ct);
        await db.SaveChangesAsync(ct);
        await BroadcastEventsAsync(village,citizen,report,completion,quests,now);
    }
}
