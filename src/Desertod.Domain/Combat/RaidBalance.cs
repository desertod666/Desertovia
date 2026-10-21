using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public sealed record RaidReadiness(int Guards,int Reserves,double Power,double Budget);

public static class RaidBalance
{
    public const int MaximumUnits=48;
    public static readonly string[] Compositions = ["mixed", "swarm", "elite", "boss"];
    public static RaidReadiness Assess(Village village,IReadOnlyList<Citizen> citizens,IReadOnlySet<Guid> online,DateTimeOffset now,int waves=1)
    {
        var healthy=citizens.Where(c=>RaidSimulation.CanFight(c,now)).ToList();
        var guards=healthy.Where(c=>online.Contains(c.Id)).ToList();
        // Readiness uses present capable residents, never a retired duty assignment.
        var readiness=guards.Sum(c=>CombatRules.Power(c)
            * Math.Sqrt((32+c.Stats.Endurance*4)/44d)
            / Math.Sqrt(1-(ArmoryCatalog.Armor(c.Armor)?.Protection??0))
            * (.65+.35*Math.Clamp(c.CurrentEnergy(now,village)/Math.Max(1,c.MaximumEnergy),0,1))
            * (village.RaidAdaptation.Participation.FirstOrDefault(p=>p.CitizenId==c.Id) is { } history
                ? .65+.35*Math.Clamp(history.Encounters/(double)Math.Max(1,village.RaidAdaptation.Recent.Count),0,1):1))
            *(village.Has(BuildingKind.Barracks)?RaidRules.BarracksMultiplier:1);
        var storedWeapons=ArmoryCatalog.Weapons.Sum(w=>Math.Min(guards.Count,village.Stock(w.Kind))*w.Power);
        readiness+=Math.Min(guards.Count*2,storedWeapons*.25);
        var shelter=village.Fortifications.Count(f=>f.Blocks)>0?6:0;
        var scale=waves switch{>=3=>1.2,2=>1.05,_=>.9};
        var ceiling=Math.Clamp(180+readiness*1.5+village.Structures.Count*2,240,1200);
        var budget=Math.Clamp((readiness+6+shelter)*scale*village.RaidAdaptation.Coefficient,8,ceiling);
        return new(guards.Count,0,readiness,budget);
    }
    public static IReadOnlyList<RaidApproach> Approaches(Village village)
    {
        var nav=new RaidNavigation(village);
        var result=new List<RaidApproach>();
        foreach(var sector in RaidTactics.Sectors)
        {
            try
            {
                var entry=nav.Entry(sector);
                if(nav.Next(entry,WorldMap.Places[WorldMap.Camp],true) is not null)
                    result.Add(new(sector,[new(){X=(int)Math.Floor(entry.X),Y=(int)Math.Floor(entry.Y)}]));
            }
            catch(GameRuleException) { /* A blocked side is not a reason to discard other valid approaches. */ }
        }
        return result;
    }
    public static string Variant(string composition,int index) => composition=="boss"?"boss":composition=="swarm"?"weak":composition=="elite"?"strong":index*37%100<35?"weak":index*37%100<55?"strong":"regular";
    private static string AffordableVariant(EnemyKind enemy,string composition,int index,double remaining)
    {
        var desired=Variant(composition,index);
        if(index>0||EnemyCatalog.Get(enemy).Power*Math.Pow(Factor(desired),2)<=remaining)return desired;
        return EnemyCatalog.Get(enemy).Power<=remaining?"regular":"weak";
    }
    public static double Factor(string variant)=>variant=="boss"?3.2:variant=="strong"?1.7:variant=="weak"?.65:1;
    public static int Count(EnemyKind enemy,string composition,double budget)
    {
        composition=NormalizeComposition(enemy,composition,budget);
        var count=0;var remaining=budget;
        while(count<(composition=="boss"?3:MaximumUnits))
        {
            var cost=EnemyCatalog.Get(enemy).Power*Math.Pow(Factor(AffordableVariant(enemy,composition,count,remaining)),2);
            if(remaining<cost&&count>0)break;
            count++;remaining-=cost;
        }
        return count;
    }
    public static string NormalizeComposition(EnemyKind enemy,string composition,double budget) =>
        composition=="boss" && budget<EnemyCatalog.Get(enemy).Power*Math.Pow(Factor("boss"),2)?"elite":composition;
    public static RaidBattle Prepare(Village village,Raid raid,IReadOnlyList<Citizen> citizens,
        IReadOnlyCollection<string> completed,IReadOnlySet<Guid> online,DateTimeOffset now,string? sector=null,string? composition=null,RaidSettings? settings=null)
    {
        Fortifications.Ensure(village,completed);
        var readiness=Assess(village,citizens,online,now,raid.Waves);
        var budget=readiness.Budget;
        var nav=new RaidNavigation(village);
        var choices=Approaches(village).Select(a=>new{a.Sector,Entry=nav.Entry(a.Sector)}).ToList();
        if(choices.Count==0)throw new GameRuleException("raid.noApproach","No reachable approach for this raid.");
        var seed=BitConverter.ToInt32(raid.Id.ToByteArray(),0)&int.MaxValue;
        var config=settings?.Copy()??new RaidSettings {Enemy=raid.Enemy,Count=composition=="boss"?3:MaximumUnits,
            Preset=NormalizeComposition(raid.Enemy,composition is "swarm" or "elite" or "mixed" or "boss"?composition:new[]{"swarm","elite","mixed"}[seed%3],budget),
            Tactic=seed%2==0?"entry":"breach",
            PropertyLimit=Math.Clamp(180+village.Structures.Count*3,180,360),
            WallLimit=Math.Clamp(village.Fortifications.Count*12,300,600),
            LootLimit=24,TheftFraction=.02,
            DurationSeconds=composition=="boss"?360:240,RetreatSurvivorFraction=composition=="boss"?0:composition=="elite"?.15:composition=="swarm"?.4:.25};
        sector=config.Sector??sector;
        var plan=RaidSettingsPlanner.Preview(config,budget,ownerManualAllowed:settings is not null,legacyFallback:settings is null);
        var chosen=sector is null?choices[seed%choices.Count]:choices.FirstOrDefault(s=>s.Sector==sector)
            ??throw new GameRuleException("raid.noApproach","The selected approach is no longer reachable.");
        var battle=new RaidBattle{Id=raid.Id,VillageId=village.Id,Sector=chosen.Sector,Seed=seed,CombatRulesVersion=CombatantRules.CurrentVersion,
            ScheduledAt=raid.StrikesAt,StartsAt=raid.StrikesAt,LastTickAt=raid.StrikesAt,EndsAt=raid.StrikesAt.AddSeconds(config.DurationSeconds),
            ThreatBudget=config.Mode=="manual"?plan.ActualCost:budget*config.BudgetMultiplier,ReadyDefenders=readiness.Guards,AvailableReserves=readiness.Reserves,
            Composition=config.Preset,Tactic=config.Tactic=="mixed"?(seed%2==0?"entry":"breach"):config.Tactic,Settings=config,
            ManualOverride=config.Mode=="manual"};
        battle.Event("readiness",string.Join(',',citizens.Where(c=>online.Contains(c.Id)&&RaidSimulation.CanFight(c,now)).Select(c=>c.Id.ToString("N"))),now);
        foreach(var unit in plan.Units)
        {
            var i=battle.Units.Count;
            var offset=(i%7-3)*.8;
            var row=(i/7)*.8;
            var spawn=chosen.Sector=="west"?new TilePoint(chosen.Entry.X-row,chosen.Entry.Y+offset)
                :new TilePoint(chosen.Entry.X+offset,chosen.Entry.Y+(chosen.Sector=="north"?-row:row));
            var p=nav.NearestFree(spawn,p=>nav.IsOutside(p,chosen.Sector) && battle.Units.All(u=>u.Position.DistanceTo(p)>=.7));
            battle.Units.Add(new(){Enemy=unit.Enemy,Variant=unit.Variant,X=p.X,Y=p.Y,SpawnX=p.X,SpawnY=p.Y,
                Health=unit.Health,MaximumHealth=unit.Health,
                Attack=unit.Attack,Speed=unit.Speed,
                NextAttackAt=raid.StrikesAt.AddSeconds(2+i%3)});
        }
        foreach(var store in village.Storages)
        foreach(var good in store.Goods.Where(g=>g.Amount>0))
            battle.LootBudgets.Add(new(){PlotId=store.PlotId,Kind=good.Kind,Limit=(int)Math.Floor(good.Amount*config.TheftFraction)});
        raid.EngineVersion=2;raid.Count=battle.Units.Count;raid.EntrySector=chosen.Sector;
        raid.ApproachPath=[]; // Old visual interpolation must not run over live server actors.
        return battle;
    }
}
