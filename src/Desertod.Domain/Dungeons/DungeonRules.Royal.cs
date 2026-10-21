using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Domain.Dungeons;

public static partial class DungeonRules
{
    private static void PrepareRoyal(DungeonRun run,Village village,IReadOnlyList<Citizen> residents,DateTimeOffset now)
    {
        RoyalInjury.Refresh(village,now,residents);
        if(TsarIsDown(village,now))throw new GameRuleException("dungeon.injured","Wait for the wound to heal before joining a dungeon.");
        var body=RoyalCombatBody.Resolve(village,residents,now);var citizen=body.Citizen;
        RequireRoyalReady(citizen,now);
        if(citizen.DungeonRunId is {} other&&other!=run.Id)throw new GameRuleException("dungeon.busy","This body is already in a dungeon.");
        if(citizen.WorkState(now)==WorkState.Working)CycleEngine.Cancel(citizen,now,village);
        run.Tsar.RoyalBodyCitizenId=body.IsResident?citizen.Id:null;
        run.Tsar.CombatClass=HeroClass.Brawler;
        if(body.IsResident)
        {
            village.TsarSpot=citizen.Trip?.PositionAt(now)??citizen.Spot??citizen.IdleSpot??village.TsarSpot;
            village.TsarTrip=CopyTrip(citizen.Trip);
            citizen.DungeonRunId=run.Id;
        }
        TakeGear(citizen,village,HeroClassCatalog.Get(HeroClass.Brawler),run.Tsar);body.Commit();
    }

    public static RoyalCombatBody? RoyalBody(DungeonRun run,Village village,IEnumerable<Citizen> citizens,DateTimeOffset now)=>
        run.CombatRulesVersion==CombatantRules.CurrentVersion&&run.Kind==DungeonRunKind.Group&&run.Tsar.AccountId==village.OwnerAccountId
            ?RoyalCombatBody.ForSource(village,run.Tsar.RoyalBodyCitizenId,citizens,now):null;

    private static bool RoyalCanMarch(DungeonRun run,Village village,IEnumerable<Citizen> citizens,DateTimeOffset now)=>
        RoyalBody(run,village,citizens,now)?.Citizen is {} body&&CanMarch(body,now);

    private static void RequireRoyalReady(Citizen citizen,DateTimeOffset now)
    {
        SafeRest.RequireActive(citizen.RestState);HospitalRules.RequireReady(citizen,now);
        if(citizen.Status!=CitizenStatus.Active)throw new GameRuleException("citizen.dormant","Dormant citizens cannot work.");
        if(CombatRules.IsInjured(citizen,now))throw new GameRuleException("dungeon.injured","You are wounded; a remedy heals it at once.");
    }

    public static void PickRoyalClass(DungeonRun run,Village village,IEnumerable<Citizen> citizens,HeroClass heroClass,DateTimeOffset now)
    {
        RequireGathering(run,now);
        if(run.CombatRulesVersion!=CombatantRules.CurrentVersion)throw new GameRuleException("dungeon.classUnavailable","The existing call keeps its original rules.");
        var body=RoyalBody(run,village,citizens,now)??throw new GameRuleException("dungeon.busy","The admitted body is no longer available.");
        RequireRoyalReady(body.Citizen,now);
        if(heroClass==HeroClass.Voivode||!AvailableClasses(body.Citizen,village,run.Tsar).Contains(heroClass))
            throw new GameRuleException("dungeon.classUnavailable","This class needs gear or a craft you do not have.");
        ReturnGear(body.Citizen,village,run.Tsar);
        run.Tsar.CombatClass=heroClass;
        TakeGear(body.Citizen,village,HeroClassCatalog.Get(heroClass),run.Tsar);body.Commit();
    }

    private static void ReleaseRoyal(DungeonRun run,Village village,IEnumerable<Citizen> citizens,DateTimeOffset now)
    {
        if(run.CombatRulesVersion!=CombatantRules.CurrentVersion||run.Tsar.Settled)return;
        if(RoyalBody(run,village,citizens,now) is {} body)
        {
            ReturnGear(body.Citizen,village,run.Tsar);
            if(body.Citizen.DungeonRunId==run.Id)body.Citizen.DungeonRunId=null;
            SyncRoyalLocation(run,village,citizens,now);body.Commit();
        }
        run.Tsar.Settled=true;
    }

    private static void SyncRoyalLocation(DungeonRun run,Village village,IEnumerable<Citizen> citizens,DateTimeOffset now)
    {
        if(run.CombatRulesVersion!=CombatantRules.CurrentVersion||run.Tsar.RoyalBodyCitizenId is not {} id)return;
        if(citizens.FirstOrDefault(c=>c.Id==id&&c.AccountId==village.OwnerAccountId) is not {} citizen)return;
        citizen.Trip=CopyTrip(village.TsarTrip);
        citizen.Spot=village.TsarTrip?.PositionAt(now)??village.TsarSpot;
        citizen.IdleSpot=village.TsarTrip?.To??village.TsarSpot;
    }

    private static void TravelRoyal(DungeonRun run,Village village,IEnumerable<Citizen> citizens,TilePoint destination,DateTimeOffset now,bool waitIfBlocked=false)
    {
        var body=RoyalBody(run,village,citizens,now);
        if(body is {IsResident:true})
        {
            // Reuse the ordinary movement rule, including medical speed exactly once.
            var citizen=body.Citizen;citizen.Trip=CopyTrip(village.TsarTrip);citizen.Spot=village.TsarSpot;
            citizen.IdleSpot=village.TsarTrip?.PositionAt(now)??village.TsarSpot;
            citizen.SetOut(destination,village,now,waitIfBlocked:waitIfBlocked);citizen.IdleSpot=destination;
            village.TsarTrip=CopyTrip(citizen.Trip);village.TsarSpot=destination;
        }
        else village.TsarGoTo(destination,now,waitIfBlocked:waitIfBlocked);
        SyncRoyalLocation(run,village,citizens,now);
    }

    private static Trip? CopyTrip(Trip? trip)=>trip is null?null:new()
    {
        Id=trip.Id,FromX=trip.FromX,FromY=trip.FromY,ToX=trip.ToX,ToY=trip.ToY,SetOutAt=trip.SetOutAt,
        SpeedMultiplier=trip.SpeedMultiplier,RouteEndsAt=trip.RouteEndsAt,BlockedAt=trip.BlockedAt,BlockReason=trip.BlockReason,
        LastResumeRequestId=trip.LastResumeRequestId,LastResumeFingerprint=trip.LastResumeFingerprint,
        Waypoints=trip.Waypoints.Select(p=>new TripWaypoint{X=p.X,Y=p.Y,Cost=p.Cost}).ToList(),
    };
}
