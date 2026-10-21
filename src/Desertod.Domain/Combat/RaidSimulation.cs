using System.Diagnostics;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public sealed record RaidTickMetrics(int Steps,double BacklogMs,double ElapsedMs,int PathSearches,int PathNodes,int BlockedPaths);

/// <summary>Authoritative, bounded steps. No aggregate victory calculation and no network/model calls.</summary>
public static class RaidSimulation
{
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(1);
    public const int MaximumCatchupSteps = 3;
    public const int PlunderDamageLimit = 180;
    public const int WallDamageLimit = 300;
    private static int PropertyLimit(RaidBattle battle)=>battle.Settings?.PropertyLimit??PlunderDamageLimit;
    private static int WallLimit(RaidBattle battle)=>battle.Settings?.WallLimit??WallDamageLimit;
    private static void Contact(RaidBattle battle,DateTimeOffset at)
    {battle.FirstContactAt??=at;battle.LastContactAt=at;}
    private static double WallDamage(RaidBattle battle)=>battle.Damage.Where(d=>d.PlotId.StartsWith("wall:",StringComparison.Ordinal)).Sum(d=>d.Amount);

    public static bool Tick(Village village,Raid raid,RaidBattle battle,IReadOnlyList<Citizen> people,
        IReadOnlySet<Guid> online,DateTimeOffset now,Action<RaidTickMetrics>? observe=null,IReadOnlySet<Guid>? reservedAccounts=null)
    {
        if(battle.FinishedAt is not null || now < battle.StartsAt || now <= battle.LastTickAt)return false;
        var measuredAt=Stopwatch.GetTimestamp();
        var backlogMs=(now-battle.LastTickAt).TotalMilliseconds;
        // A server outage freezes the fight rather than charging hours of unseen attacks to players.
        if(now-battle.LastTickAt>TimeSpan.FromSeconds(15))
        {
            var pause=now-battle.LastTickAt-Step;
            battle.EndsAt+=pause;battle.LastTickAt+=pause;
            foreach(var u in battle.Units)
            {
                u.NextAttackAt+=pause;
                if(u.StrikeAt is not null)u.StrikeAt+=pause;
                if(u.SpecialReadyAt is not null)u.SpecialReadyAt+=pause;
            }
            foreach(var f in battle.Fighters)f.NextAttackAt+=pause;
            battle.Event("resumed","",now);
        }
        var steps=Math.Min(MaximumCatchupSteps,(int)Math.Floor((now-battle.LastTickAt).TotalSeconds));
        if(steps==0)return false;
        var nav=new RaidNavigation(village);
        var citizens=people.ToDictionary(c=>c.Id);
        for(var step=0;step<steps;step++)
        {
            var at=battle.LastTickAt+Step;
            Join(village,battle,people,online,at,nav,reservedAccounts);
            if(battle.Phase=="Warning")
            {battle.Phase="Approaching";battle.Event("arrived",battle.Sector,at);foreach(var u in battle.Units)u.State="Approaching";}
            var living=battle.Units.Count(u=>u.Alive);
            if(at>=battle.EndsAt || PropertyLimit(battle)>0&&battle.DamageDone>=PropertyLimit(battle)
                || living<=battle.Units.Count*(battle.Settings?.RetreatSurvivorFraction??.3))
            {
                if(battle.Phase!="Retreating")battle.Event("retreat",battle.Sector,at);
                battle.Phase="Retreating";
            }
            foreach(var f in battle.Fighters.Where(f=>f.Active&&f.Health>0))
                if(f.Mode=="retreating") { BattleActions.StepRetreat(f,nav); if(f.IsTsar)village.TsarSpot=f.Position; else if(citizens.TryGetValue(f.CitizenId,out var withdrawing)){withdrawing.Spot=f.Position;withdrawing.IdleSpot=f.Position;withdrawing.Trip=null;} }
                else if(f.IsTsar) ActTsar(village,battle,f,nav,at);
                else if(citizens.TryGetValue(f.CitizenId,out var c))FightCitizen(village,raid,battle,f,c,citizens,nav,at);
            nav.BeginCrowd(battle,EnemyCatalog.Get(raid.Enemy));
            foreach(var unit in battle.Units.Where(u=>u.Alive))
            {
                Enemy(village,raid,battle,unit,citizens,nav,at);
                if(village.DefeatedAt is not null)
                {
                    foreach(var survivor in battle.Units.Where(u=>u.Alive)){survivor.ExitedAt=at;survivor.State="Dispersed";}
                    battle.Event("defeated","castle",at);break;
                }
            }
            nav.ResolveCrowd();
            battle.LastTickAt=at;battle.Revision++;
            if(battle.Units.All(u=>!u.Alive))
            {
                Finish(village,raid,battle,citizens,at);
                break;
            }
        }
        observe?.Invoke(new(steps,backlogMs,Stopwatch.GetElapsedTime(measuredAt).TotalMilliseconds,nav.PathSearches,nav.PathNodes,nav.BlockedPaths));
        return true;
    }

    public static bool CanFight(Citizen c,DateTimeOffset at) => c.Status==CitizenStatus.Active
        && c.RestState!=RestState.Resting
        && c.RecoveryPausedAt is null && !HospitalRules.IsAdmitted(c,at) && !CombatRules.IsInjured(c,at) && !(c.ExhaustedUntil>at)
        && c.Recovery?.Available!=false && c.DungeonRunId is null;

    private static void Join(Village village,RaidBattle battle,IReadOnlyList<Citizen> people,IReadOnlySet<Guid> online,
        DateTimeOffset at,RaidNavigation nav,IReadOnlySet<Guid>? reservedAccounts)
    {
        foreach(var c in people)
        {
            if (battle.Fighters.Any(f => f.IsTsar && f.AccountId == c.AccountId)) continue;
            var position=c.RescueJourney is {CompletedAt:null} rescue?rescue.PositionAt(at)
                :c.Trip?.PositionAt(at)??c.Spot??c.IdleSpot??WorldMap.Places[WorldMap.Camp];
            var inDanger=reservedAccounts?.Contains(c.AccountId)!=true&&online.Contains(c.Id)&&CanFight(c,at)&&battle.Units.Any(u=>u.Alive&&u.Position.DistanceTo(position)<4);
            var existing=battle.Fighters.SingleOrDefault(f=>f.CitizenId==c.Id);
            if(existing is not null)
            {
                if(c.RescueJourney is {CompletedAt:null})
                {existing.X=position.X;existing.Y=position.Y;existing.DestinationX=null;existing.DestinationY=null;}
                if(existing.Mode=="withdrawn"){existing.Active=false;continue;}
                if(existing.Mode=="retreating"){existing.Active=CanFight(c,at)&&existing.Health>0;continue;}
                existing.Guard=false;existing.Active=CanFight(c,at)&&existing.Health>0&&(existing.Active||inDanger);
                existing.Weapon=c.Weapon;existing.Armor=c.Armor;existing.Arrows=c.Arrows;
                continue;
            }
            if(!inDanger)continue;
            if(c.Weapon is null)CombatRules.Arm(c,village);
            var p=nav.NearestFree(position);
            battle.Fighters.Add(new(){CitizenId=c.Id,X=p.X,Y=p.Y,Guard=false,Active=true,
                Health=32+c.Stats.Endurance*4,MaximumHealth=32+c.Stats.Endurance*4,Weapon=c.Weapon,Armor=c.Armor,Arrows=c.Arrows,NextAttackAt=at.AddSeconds(1)});
        }
    }

    private static void FightCitizen(Village village,Raid raid,RaidBattle battle,RaidFighter fighter,
        Citizen citizen,IReadOnlyDictionary<Guid,Citizen> battlePeople,RaidNavigation nav,DateTimeOffset at)
    {
        var carrying=citizen.RescueJourney is {CompletedAt:null};
        if (!carrying && MoveOrder(fighter, nav)) return;
        var weapon=CombatRules.UsableWeapon(citizen);
        var range=weapon?.Kind==ResourceKind.Bow?5:1.45;
        var target=battle.Units.FirstOrDefault(u=>u.Id==fighter.TargetUnitId&&u.Alive);
        var selected=target is not null;
        if(!selected)fighter.TargetUnitId=null;
        // No patrol, post or implicit chase. Local self-defense never moves the player.
        target??=battle.Units.Where(u=>u.Alive&&u.Position.DistanceTo(fighter.Position)<=range&&nav.ClearShot(fighter.Position,u.Position))
            .OrderBy(u=>u.Position.DistanceTo(fighter.Position)).FirstOrDefault();
        if(target is null)return;
        var distance=target.Position.DistanceTo(fighter.Position);
        if(distance>range || !nav.ClearShot(fighter.Position,target.Position))
        {
            if(selected&&fighter.Mode!="hold"&&!carrying)nav.Move(fighter,target.Position,1);
            return;
        }
        if(fighter.NextAttackAt>at)return;
        fighter.NextAttackAt=at.AddSeconds(weapon?.Kind==ResourceKind.Bow?2.5:2);
        var power=CombatRules.Power(citizen);
        if(weapon?.Arrows>0){citizen.SpendArrows(1);fighter.ArrowsSpent++;}
        fighter.Arrows=citizen.Arrows;
        var multiplier=village.Has(BuildingKind.Barracks)?RaidRules.BarracksMultiplier:1;
        var hit=Math.Min(target.Health,Math.Max(1,power*multiplier*.85*PreparedMeals.AttackMultiplier(citizen,at)));
        Contact(battle,at);
        target.Health=Math.Max(0,target.Health-hit);fighter.DamageDealt+=hit;
        fighter.RangedDamage=(fighter.RangedDamage??0)+(weapon?.Kind==ResourceKind.Bow?hit:0);
        if(battle.Phase=="Approaching")battle.Phase="Fighting";
        if(target.Health>0)return;
        target.State="Dead";target.DiedAt=at;fighter.Kills++;
        battle.Event("enemy_down",target.Id.ToString(),at);
    }

    private static bool MoveOrder(RaidFighter fighter, RaidNavigation nav)
    {
        if (fighter.DestinationX is not {} x || fighter.DestinationY is not {} y) return false;
        var goal = new TilePoint(x,y);
        nav.Move(fighter,goal,1);
        if(fighter.Position.DistanceTo(goal)<.01)
        {fighter.DestinationX=null;fighter.DestinationY=null;fighter.Mode="hold";}
        return true;
    }

    private static void ActTsar(Village village, RaidBattle battle, RaidFighter fighter, RaidNavigation nav, DateTimeOffset at)
    {
        if (fighter.AccountId != village.OwnerAccountId || village.TsarInjuredUntil > at)
        { fighter.Active=false; return; }
        if (MoveOrder(fighter,nav)) { village.TsarSpot=fighter.Position; return; }
        var target=battle.Units.FirstOrDefault(u=>u.Id==fighter.TargetUnitId&&u.Alive);
        target ??= battle.Units.Where(u=>u.Alive&&u.Position.DistanceTo(fighter.Position)<=1.0)
            .OrderBy(u=>u.Position.DistanceTo(fighter.Position)).FirstOrDefault();
        if (target is null) return;
        if (target.Position.DistanceTo(fighter.Position)>1.0 || !nav.ClearShot(fighter.Position,target.Position))
        { if(fighter.Mode!="hold") nav.Move(fighter,target.Position,1); village.TsarSpot=fighter.Position; return; }
        if (fighter.NextAttackAt>at) return;
        fighter.NextAttackAt=at.AddSeconds(1);
        var hit=Math.Min(target.Health,Desertod.Domain.Dungeons.HeroClassCatalog.VoivodeDamage);
        Contact(battle,at);
        target.Health=Math.Max(0,target.Health-hit);fighter.DamageDealt+=hit;

        battle.Event("hit",target.Id.ToString(),at);
        if(target.Health<=0){target.State="Dead";target.DiedAt=at;fighter.Kills++;battle.Event("enemy_down",target.Id.ToString(),at);}
        if(battle.Phase=="Approaching")battle.Phase="Fighting";
    }

    private static void Enemy(Village village,Raid raid,RaidBattle battle,RaidUnit unit,
        IReadOnlyDictionary<Guid,Citizen> people,RaidNavigation nav,DateTimeOffset at)
    {
        var enemy=EnemyCatalog.Get(unit.Enemy??raid.Enemy);
        if(battle.Phase=="Retreating"||unit.State=="Retreating")
        {
            unit.State="Retreating";unit.StrikeAt=null;unit.StrikeX=null;unit.StrikeY=null;unit.StrikeTarget=null;unit.TargetKind="exit";unit.TargetId=null;
            var exit=new TilePoint(unit.SpawnX,unit.SpawnY);
            if(unit.Position.DistanceTo(exit)<1)
            {
                Escape(village,raid,battle,unit,enemy);unit.ExitedAt=at;unit.State="Escaped";return;
            }
            if(!MoveUnit(nav,unit,exit,1,enemy.Damage>0&&WallDamage(battle)<WallLimit(battle),true,out var blocked))unit.BlockedSeconds++;
            if(blocked is not null)BreakWall(unit,blocked,battle,enemy,at);
            if(at>battle.EndsAt.AddSeconds(60))
            {
                // Trapped raiders disperse without taking goods through an impassable wall.
                unit.ExitedAt=at;unit.State="Dispersed";
            }
            return;
        }
        var index=battle.Units.IndexOf(unit);
        var profile=MonsterBrain.Profile(enemy,unit.Variant,index);
        if(unit.Variant=="boss")
        {
            var phase=battle.Settings?.BossSecondPhase!=false&&unit.Health<=unit.MaximumHealth*.5?2:1;
            if(unit.BossPhase!=phase){unit.BossPhase=phase;battle.Event("boss_phase",unit.Id.ToString(),at);}
            if(unit.StrikeAt is not null)
            {
                if(unit.StrikeAt<=at)
                {
                    if(unit.StrikeTarget is {} plot && unit.StrikeX is {} sx && unit.StrikeY is {} sy)
                    {
                        var impact=new TilePoint(sx,sy);
                        if(Targets(village).Contains(plot)&&unit.Position.DistanceTo(impact)<=2.5)
                            DamageProperty(village,battle,unit,enemy,plot,people,at,3);
                        foreach(var target in battle.Fighters.Where(f=>f.Active&&f.Health>0&&f.Position.DistanceTo(impact)<=1.65&&nav.ClearShot(impact,f.Position)))
                            HitFighter(village,battle,enemy,target,people,at,unit.Attack*.75);
                        battle.Event("boss_strike",unit.Id.ToString(),at);
                    }
                    unit.StrikeAt=null;unit.StrikeX=null;unit.StrikeY=null;unit.StrikeTarget=null;
                    unit.SpecialReadyAt=at.AddSeconds(phase==2?(battle.Settings?.BossSecondCooldownSeconds??6):(battle.Settings?.BossCooldownSeconds??9));
                }
                return;
            }
        }
        var centre=WorldMap.Places[WorldMap.Camp];
        var objectives=Targets(village).Where(p=>CombatRules.DamageOf(village,p)<CombatRules.MaximumDamage)
            .Where(p=>RuinRules.TargetYard(p).DistanceTo(unit.Position)<12)
            .Where(p=>unit.TargetId==p||battle.Units.Count(u=>u.Alive&&u.TargetKind=="building"&&u.TargetId==p)<5)
            .Select(p=>new MonsterObjective("building",p,RuinRules.TargetYard(p),
                (village.Storages.Any(store=>store.PlotId==p&&store.Goods.Any(g=>g.Amount>0))?(battle.Tactic=="plunder"?16:8):6)*(battle.Tactic=="hunt"?.2:1),
                battle.Units.Count(u=>u.Id!=unit.Id&&u.Alive&&u.TargetKind=="building"&&u.TargetId==p))).ToList();
        if(enemy.Damage>0&&profile.Breach&&index%6==0&&WallDamage(battle)<WallLimit(battle))
            objectives.AddRange(village.Fortifications.Where(w=>w.Blocks&&unit.Position.DistanceTo(new(w.X+.5,w.Y+.5))<7)
                .Where(w=>unit.TargetId==w.Id||battle.Units.Count(u=>u.Alive&&u.TargetKind=="wall"&&u.TargetId==w.Id)<2)
                .Select(w=>new MonsterObjective("wall",w.Id,new(w.X+.5,w.Y+.5),profile.Breach?3:1,
                    battle.Units.Count(u=>u.Id!=unit.Id&&u.Alive&&u.TargetKind=="wall"&&u.TargetId==w.Id))));
        // Набег разоряет имущество. Игрок отвлекает монстра ударом или действительно закрывает ему путь.
        foreach(var f in battle.Fighters.Where(f=>f.Active&&f.Health>0&&unit.Position.DistanceTo(f.Position)<=(battle.Tactic=="hunt"?8:3.5)
            &&nav.ClearShot(unit.Position,f.Position)))
        {
            var distance=unit.Position.DistanceTo(f.Position);
            var provoking=f.TargetUnitId==unit.Id;
            if(provoking||distance<=unit.Range+.25||battle.Tactic=="hunt")
                objectives.Add(new(f.IsTsar?"tsar":"citizen",f.ActorId.ToString(),f.Position,distance<.6?20:provoking?12:battle.Tactic=="hunt"?10:3,
                    battle.Units.Count(u=>u.Id!=unit.Id&&u.Alive&&u.TargetId==f.ActorId.ToString())));
        }
        var choice=MonsterBrain.Choose(profile,unit.Position,unit.TargetId,objectives);
        if(choice?.Kind is "citizen" or "tsar")
        {
            var victim=battle.Fighters.First(f=>f.ActorId.ToString()==choice.Id);
            unit.TargetKind=choice.Kind;unit.TargetId=choice.Id;unit.State="Fighting";battle.Phase="Fighting";
            if(unit.Position.DistanceTo(victim.Position)>unit.Range||!nav.ClearShot(unit.Position,victim.Position))
            {
                MoveUnit(nav,unit,victim.Position,1,enemy.Damage>0&&WallDamage(battle)<WallLimit(battle),unit.Variant=="boss"||profile.Breach&&battle.Tactic=="breach",out var barrier);
                if(barrier is not null)BreakWall(unit,barrier,battle,enemy,at);
                return;
            }
            if(unit.NextAttackAt>at)return;
            unit.NextAttackAt=at.AddSeconds(3);
            HitFighter(village,battle,enemy,victim,people,at,unit.Attack);
            return;
        }
        if(choice?.Kind=="wall")
        {
            unit.TargetKind="wall";unit.TargetId=choice.Id;
            MoveUnit(nav,unit,choice.Position,1,true,true,out var barrier);
            if(barrier is not null)BreakWall(unit,barrier,battle,enemy,at);
            return;
        }
        var building=choice?.Kind=="building"?choice.Id:null;
        var goal=centre;
        if(building is not null)
        {
            goal=building==RuinRules.RoyalBridge?nav.NearestFree(RuinRules.TargetYard(building)):RuinRules.TargetYard(building);unit.TargetKind="building";unit.TargetId=building;
            if(unit.Position.DistanceTo(goal)<1.4 && nav.ClearShot(unit.Position,goal))
            {
                unit.State="Raiding";battle.Phase="Raiding";
                if(unit.NextAttackAt>at)return;
                unit.NextAttackAt=at.AddSeconds(3);
                if(unit.Variant=="boss"&&battle.Settings?.BossStrike!=false)
                {
                    if(unit.SpecialReadyAt>at)return;
                    unit.StrikeTarget=building;unit.StrikeX=goal.X;unit.StrikeY=goal.Y;
                    unit.StrikeAt=at.AddSeconds(unit.BossPhase==2?(battle.Settings?.BossSecondWarningSeconds??2):(battle.Settings?.BossWarningSeconds??3));unit.State="Windup";
                    battle.Event("boss_windup",unit.Id.ToString(),at);
                    return;
                }
                DamageProperty(village,battle,unit,enemy,building,people,at,1);
                if(unit.PlunderPlot is null&&village.Storages.Any(store=>store.PlotId==building))unit.PlunderPlot=building;
                if((enemy.Damage==0||CombatRules.DamageOf(village,building)>=50)&&unit.PlunderPlot is not null)unit.State="Retreating";
                return;
            }
        }
        else{unit.TargetKind="centre";unit.TargetId=null;}
        unit.State="Approaching";
        if(unit.Position.DistanceTo(goal)<1 && building is null)
        {
            // At the rally point choose an actual building to raid; the fire itself is never attacked.
            var targets=Targets(village).Where(p=>CombatRules.DamageOf(village,p)<100).ToArray();
            var p=targets.Where(p=>battle.Units.Count(u=>u.Alive&&u.TargetKind=="building"&&u.TargetId==p)<5)
                .OrderBy(p=>RuinRules.TargetYard(p).DistanceTo(unit.Position)).FirstOrDefault();
            if(p is not null){goal=p==RuinRules.RoyalBridge?nav.NearestFree(RuinRules.TargetYard(p)):RuinRules.TargetYard(p);unit.TargetKind="building";unit.TargetId=p;}
            else if(targets.Length==0){unit.State="Retreating";return;}
        }
        var moved=MoveUnit(nav,unit,goal,1,enemy.Damage>0&&WallDamage(battle)<WallLimit(battle),unit.Variant=="boss"||profile.Breach&&battle.Tactic=="breach"||unit.BlockedSeconds>=6,out var wall);
        if(wall is not null)BreakWall(unit,wall,battle,enemy,at);
        if(!moved)unit.BlockedSeconds++;
        // Затор — причина сменить маршрут/пролом, а не покинуть бой через10секунд.
    }

    private static bool MoveUnit(RaidNavigation nav,RaidUnit unit,TilePoint goal,double seconds,bool breach,bool prefer,out Fortification? wall)
    {
        return nav.Move(unit,goal,seconds,breach,prefer,out wall);
    }

    private static void HitFighter(Village village,RaidBattle battle,EnemyDefinition enemy,RaidFighter victim,
        IReadOnlyDictionary<Guid,Citizen> people,DateTimeOffset at,double attack)
    {
        if (victim.IsTsar)
        {
            var before=victim.Health;
            victim.Health=Math.Max(0,victim.Health-attack);
            victim.DamageTaken+=before-victim.Health;
            battle.PlayerHealthLost+=before-victim.Health;Contact(battle,at);
            if(victim.Health<=0)
            {
                victim.Active=false;victim.Wounded=true;
                village.TsarInjuredUntil=at.AddMinutes(enemy.InjuryMinutes);
                village.TsarWoundedAt=at;village.TsarHospitalizedAt=null;village.TsarRescueByCitizenId=null;village.TsarRescueRewardEligible=true;
                village.TsarRescueRequestedAt=null;
                village.TsarSpot=victim.Position;village.TsarTrip=null;
                battle.Event("wounded",victim.ActorId.ToString(),at);
            }
            return;
        }
        if(!people.TryGetValue(victim.CitizenId,out var c))return;
        var protection=ArmoryCatalog.Armor(c.Armor)?.Protection??0;
        var previousHealth=victim.Health;
        victim.Health=Math.Max(0,victim.Health-attack*(1-protection));
        victim.DamageTaken+=previousHealth-victim.Health;
        battle.PlayerHealthLost+=previousHealth-victim.Health;Contact(battle,at);
        if(victim.Health<=0)
        {
            victim.Active=false;victim.Wounded=true;
            CombatRules.Injure(c,TimeSpan.FromMinutes(enemy.InjuryMinutes),at,rewardEligible:true);
            c.IdleSpot=victim.Position;c.Spot=victim.Position;c.Trip=null;
            if(c.RescuePatientId is {} patientId)RescueRules.Cancel(c,people.GetValueOrDefault(patientId),at,village);
            battle.Event("wounded",c.Id.ToString(),at);
        }
    }

    private static void DamageProperty(Village village,RaidBattle battle,RaidUnit unit,EnemyDefinition enemy,
        string building,IReadOnlyDictionary<Guid,Citizen> people,DateTimeOffset at,double multiplier)
    {
        var damage=Math.Max(0,Math.Min(PropertyLimit(battle)-(int)battle.DamageDone,
            enemy.Damage==0?0:Math.Max(1,(int)Math.Round(enemy.Damage*.25*multiplier*(battle.Settings?.PropertyMultiplier??1)*(unit.Variant is "strong" or "boss"?1.5:1)))));
        var added=damage>0?CombatRules.Damage(village,building,damage):0;
        battle.DamageDone+=added;
        if(added>0)Contact(battle,at);
        if(added>0)RuinRules.Collapse(village,building,people.Values.ToArray(),at,battle.Id);
        var damaged=battle.Damage.FirstOrDefault(d=>d.PlotId==building);
        if(damaged is null)battle.Damage.Add(new(){PlotId=building,Amount=added});else damaged.Amount+=added;
    }

    private static IEnumerable<string> Targets(Village v) => v.Structures.Select(s=>s.PlotId)
        .Concat(v.Storages.Select(s=>s.PlotId)).Concat(v.Sites.Where(s=>s.Kind!=BuildingKind.FortificationUpgrade).Select(s=>s.PlotId)).Concat(v.BridgeOpened||v.BridgeRuinedAt is not null?new[]{RuinRules.RoyalBridge}:[]).Distinct();

    private static void BreakWall(RaidUnit unit,Fortification wall,RaidBattle battle,EnemyDefinition enemy,DateTimeOffset at)
    {
        unit.State="Breaching";unit.TargetKind="wall";unit.TargetId=wall.Id;
        if(unit.NextAttackAt>at)return;
        unit.NextAttackAt=at.AddSeconds(3);
        var added=Math.Min(100-wall.Damage,Math.Min(WallLimit(battle)-(int)WallDamage(battle),
            enemy.Damage>0?Math.Max(1,(int)Math.Round(enemy.Damage*.35/wall.Strength*(battle.Settings?.PropertyMultiplier??1)*(unit.Variant=="boss"?2.5:unit.Variant=="strong"?1.5:1))):0));
        if(added<=0)return;
        wall.Damage+=added;
        Contact(battle,at);
        var damage=battle.Damage.FirstOrDefault(d=>d.PlotId==wall.Id);
        if(damage is null)battle.Damage.Add(new(){PlotId=wall.Id,Amount=added});else damage.Amount+=added;
        if(wall.Damage>=100)battle.Event("breach",wall.Id,at);
    }

    private static void Escape(Village village,Raid raid,RaidBattle battle,RaidUnit unit,EnemyDefinition enemy)
    {
        // Loot stays in its source until a living raider escapes. A killed carrier never creates a second copy.
        var store=village.Storages.FirstOrDefault(s=>s.PlotId==unit.PlunderPlot);
        if(store is null)return;
        foreach(var good in enemy.Steals)
        {
            var allowance=battle.LootBudgets.FirstOrDefault(l=>l.PlotId==store.PlotId&&l.Kind==good.Resource);
            var fractional=battle.Settings is not null?Math.Max(0,(allowance?.Limit??0)-(allowance?.Taken??0)):4;
            var taken=store.Take(good.Resource,Math.Max(0,Math.Min(Math.Min(good.Amount,fractional),
                (battle.Settings?.LootLimit??24)-raid.Stolen.Sum(g=>g.Amount))));
            if(taken<=0)continue;
            if(allowance is not null)allowance.Taken+=taken;
            var existing=raid.Stolen.FirstOrDefault(g=>g.Kind==good.Resource);
            if(existing is null)raid.Stolen.Add(new(){Kind=good.Resource,Amount=taken});else existing.Amount+=taken;
        }
    }

    public static void Cancel(Village village, Raid raid, RaidBattle battle, IReadOnlyList<Citizen> people, DateTimeOffset at)
    {
        if (battle.FinishedAt is not null) return;
        Finish(village, raid, battle, people.ToDictionary(c => c.Id), at, cancelled: true);
        foreach (var unit in battle.Units.Where(u => u.Alive))
        { unit.ExitedAt = at; unit.State = "Dispersed"; unit.TargetId = null; unit.TargetKind = null; }
        foreach (var fighter in battle.Fighters)
        { fighter.Active = false; fighter.TargetUnitId = null; }
        battle.Revision++;
    }

    private static void Finish(Village village,Raid raid,RaidBattle battle,IReadOnlyDictionary<Guid,Citizen> people,DateTimeOffset at, bool cancelled = false)
    {
        battle.Phase=cancelled?"Cancelled":"Finished";battle.FinishedAt=at;
        raid.Killed=battle.Units.Count(u=>u.Health<=0);
        raid.Defenders=battle.Fighters.Count(f=>f.DamageDealt>0);
        raid.Injured=battle.Fighters.Count(f=>f.Wounded);
        raid.DamagedBuildings=battle.Damage.Count(d=>d.Amount>0);
        raid.ResolvedAt=at;
        var active=battle.Fighters.Where(f=>f.DamageDealt>0||f.DamageTaken>0||f.Wounded).ToList();
        var healthPool=active.Sum(f=>f.MaximumHealth);
        var heavy=village.DefeatedByBattleId==battle.Id || active.Count>0&&
            (raid.Injured>=Math.Max(2,(int)Math.Ceiling(active.Count*.4)) || battle.PlayerHealthLost>=healthPool*.6);
        raid.Outcome=cancelled?RaidOutcome.Cancelled:heavy?RaidOutcome.Lost:
            battle.DamageDone>0||WallDamage(battle)>0||raid.Stolen.Count>0?RaidOutcome.PropertyDamaged:RaidOutcome.Repelled;
        raid.HeavyDefeat=heavy&&!cancelled;
        battle.Result=raid.Outcome.ToString();
        var broken=0;
        var contributors=battle.Fighters.Where(f=>f.DamageDealt>0&&!f.RewardsGranted).OrderBy(f=>f.CitizenId).ToList();
        var total=contributors.Sum(f=>f.DamageDealt);
        var remaining=raid.Killed*RaidRules.RenownPerKill;
        for(var i=0;i<contributors.Count;i++)
        {
            var f=contributors[i];if(!people.TryGetValue(f.CitizenId,out var c))continue;
            var reward=i==contributors.Count-1?remaining:(int)Math.Floor(raid.Killed*RaidRules.RenownPerKill*f.DamageDealt/Math.Max(1,total));
            remaining-=reward;c.Renown+=reward;f.RenownEarned=reward;
            if (!cancelled || raid.Killed > 0) SkillRules.AddExperience(c,Skill.Guarding,CombatRules.FightExperience);
            if(!cancelled&&!battle.ManualOverride)CombatTraining.Grant(c,f);
            // Equipment wears once per encounter, not once per server tick. Arrows were consumed by actual shots.
            var random=new Random(battle.Seed^c.Id.GetHashCode());
            if(c.Weapon is {} w && random.NextDouble()<(ArmoryCatalog.Weapon(w)?.BreakChance??0)){c.ClearWeapon();broken++;}
            if(c.Armor is {} a && random.NextDouble()<(ArmoryCatalog.Armor(a)?.BreakChance??0)){c.Armor=null;broken++;}
            f.RewardsGranted=true;
        }
        // An idle player's chosen final position survives the transition out of combat.
        // Keep paid work/travel intact; their ordinary settlement owns those destinations.
        foreach(var f in battle.Fighters.Where(f=>!f.IsTsar&&!f.Wounded))
            if(people.TryGetValue(f.CitizenId,out var c)&&c.CycleSettled)
            {c.Spot=f.Position;c.IdleSpot=f.Position;c.Trip=null;}
        foreach(var good in battle.Units.Where(u=>u.Health<=0).SelectMany(u=>EnemyCatalog.Get(u.Enemy??raid.Enemy).Loot)
            .GroupBy(g=>g.Resource))
        {
            var amount=village.Store(good.Key,good.Sum(g=>g.Amount));
            if(amount>0)raid.Loot.Add(new(){Kind=good.Key,Amount=amount});
        }
        if (!cancelled) village.AddMorale(raid.Outcome==RaidOutcome.Lost?RaidRules.LostMorale:raid.Outcome==RaidOutcome.Repelled?RaidRules.RepelledMorale:0,at);
        RaidAdaptationRules.Record(village,raid,battle,healthPool,at);
        raid.Summary=new(){ContactSeconds=battle.FirstContactAt is {} first&&battle.LastContactAt is {} last?Math.Max(0,(last-first).TotalSeconds):0,
            Participants=active.Count,PlayerHealthLost=battle.PlayerHealthLost,RemediesUsed=battle.RemediesUsed,
            ArrowsSpent=battle.Fighters.Sum(f=>f.ArrowsSpent),BrokenItems=broken,PropertyDamage=(int)battle.DamageDone,WallDamage=(int)WallDamage(battle),
            NextCoefficient=village.RaidAdaptation.Coefficient,
            Reason=cancelled?"Отменено: адаптация не меняется":battle.ManualOverride?"Ручной эксперимент: адаптация не меняется":village.RaidAdaptation.Reason};
        battle.Event(cancelled?"cancelled":"finished",raid.Outcome.ToString(),at);
        var old=village.Raids.Where(r=>!r.IsPending).OrderByDescending(r=>r.ResolvedAt).Skip(RaidRules.KeptRaids).Select(r=>r.Id).ToHashSet();
        village.Raids.RemoveAll(r=>old.Contains(r.Id));
        RaidRules.UpdateNextRaidAt(village);
    }
}
