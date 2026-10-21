using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Skills;

namespace Desertod.Domain.Combat;

/// <summary>One manual encounter belongs to one already accepted work cycle. Its report is consumed by normal work settlement.</summary>
public static class WorkEncounterRules
{
    public static DungeonRun Open(Citizen citizen,EnemyKind enemy,int seed,DateTimeOffset now)
    {
        if(CombatRules.IsInjured(citizen,now))throw new GameRuleException("citizen.injured","Wait for the wound to heal before fighting.");
        if(citizen.CycleSettled||citizen.CycleStartedAt is null||citizen.DungeonRunId is not null||citizen.CycleEncounterRunId is not null)
            throw new GameRuleException("ambush.changed","The accepted work changed.");
        var fighter=AmbushScene.Fighter(citizen);
        var run=new DungeonRun{VillageId=citizen.VillageId,Kind=DungeonRunKind.Ambush,DungeonId=AmbushScene.Map(enemy).Id,
            AmbushEnemy=enemy,WorkCitizenId=citizen.Id,WorkStartedAt=citizen.CycleStartedAt,WorkTaskId=citizen.CurrentTaskId,Seed=seed,
            Phase=DungeonPhase.Marching,OpenedAt=now,GatheringEndsAt=now,MarchEndsAt=now,Foes=1,
            Heroes=[new(){CitizenId=citizen.Id,AccountId=citizen.AccountId,Name=citizen.Name,Class=fighter.Class,ArrivesAt=now}]};
        citizen.CycleEncounterRunId=run.Id;citizen.DungeonRunId=run.Id;
        citizen.IdleSpot=citizen.Trip?.PositionAt(now)??citizen.Spot??citizen.IdleSpot;citizen.Trip=null;
        return run;
    }

    public static FightReport Settle(DungeonRun run,DungeonHero hero,Citizen citizen,DungeonHeroOutcome outcome,Random random,DateTimeOffset now)
    {
        // Hospital recovery can shift the cycle clock. The retained run link and task identify
        // the accepted work; Begin clears that link before any new cycle can be accepted.
        if(run.Kind!=DungeonRunKind.Ambush||run.WorkCitizenId!=citizen.Id||run.WorkTaskId!=citizen.CurrentTaskId
            ||citizen.CycleEncounterRunId!=run.Id||run.AmbushEnemy is not {} kind||hero.CitizenId!=citizen.Id)
            throw new GameRuleException("ambush.changed","The encounter no longer matches its accepted work.");
        if(hero.Settled)return Report(citizen)??throw new InvalidOperationException("Missing settled encounter report.");
        if(citizen.CycleSettled||citizen.DungeonRunId!=run.Id)throw new GameRuleException("ambush.changed","The accepted work changed.");
        var enemy=EnemyCatalog.Get(kind);var won=outcome.Kills>0&&!outcome.KnockedOut;
        var power=CombatRules.Power(citizen);var weapon=CombatRules.UsableWeapon(citizen)?.Kind;var armor=citizen.Armor;
        var injured=(won||outcome.KnockedOut)&&CombatRules.Wound(citizen,won?CombatRules.WonInjuryChance:1,enemy,random,now);
        var broken=CombatRules.Wear(citizen,random,spendArrows:false);
        if(weapon==ResourceKind.Bow)citizen.SpendArrows(Math.Max(0,citizen.Arrows-Math.Clamp(outcome.ArrowsLeft,0,citizen.Arrows)));
        SkillRules.AddExperience(citizen,Skill.Guarding,won?CombatRules.FightExperience:CombatRules.FightExperience/2);
        var report=new FightReport(kind,won,injured,injured?citizen.InjuredUntil:null,power,enemy.Power,weapon,armor,won?enemy.Loot:[],broken);
        citizen.CycleEncounterReportJson=JsonSerializer.Serialize(report);citizen.DungeonRunId=null;
        hero.Settled=true;hero.Kills=outcome.Kills;hero.KnockedOut=outcome.KnockedOut;hero.Killer=outcome.Killer;
        hero.Experience=won?CombatRules.FightExperience:CombatRules.FightExperience/2;
        hero.Broken.AddRange(broken);hero.Loot.AddRange(report.Loot.Select(x=>new DungeonGood{Kind=x.Resource,Amount=x.Amount}));
        return report;
    }

    public static FightReport? Report(Citizen citizen)=>citizen.CycleEncounterReportJson is {} json?JsonSerializer.Deserialize<FightReport>(json):null;
}
