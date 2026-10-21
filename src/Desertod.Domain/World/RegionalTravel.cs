using Desertod.Domain.Combat;
using Desertod.Domain.Entities;

namespace Desertod.Domain.World;

/// <summary>Authoritative paths for journeys involving the expanded land. Legacy accepted trips keep their timing.</summary>
public static class RegionalTravel
{
    public static Trip SetOut(Village village,Trip? previous,TilePoint resting,TilePoint to,DateTimeOffset now)
    {
        if(!double.IsFinite(to.X)||!double.IsFinite(to.Y))throw new GameRuleException("spot.outsideLand","This spot is beyond the land.");
        var from=previous?.PositionAt(now)??resting;
        if(!RegionalTerrain.NewLand(from)&&!RegionalTerrain.NewLand(to)&&previous?.Waypoints.Count is not >0)
        {
            BankAccess.Require(village,to);
            return Exploration.SetOut(village.Explored,previous,resting,to,now,village.Wilds?.Lookout);
        }
        var way=Find(village,from,to);
        var trip=Trip.Start(from,to,now);trip.Waypoints=way;trip.RouteEndsAt=trip.ArrivesAt;
        if(previous is not null)Exploration.Reveal(village.Explored,previous,now,village.Wilds?.Lookout);
        return trip;
    }

    public static List<TripWaypoint> Find(Village village,TilePoint from,TilePoint to)
    {
        if(!WorldMap.Land.Contains(from.ToTile())||!WorldMap.Land.Contains(to.ToTile()))throw new GameRuleException("spot.outsideLand","This spot is beyond the land.");
        // Someone already on the far bank can still move there after a bridge loss.
        // Crossing the river is decided by the actual deck geometry, not destination-bank permission alone.
        if(!village.BridgeOpened||village.BridgeRuinedAt is not null)
        {
            var seed=Wilderness.Seed(village.Id.ToString());
            if((from.X>Wilderness.RiverCenter((int)Math.Floor(from.Y),seed))!=(to.X>Wilderness.RiverCenter((int)Math.Floor(to.Y),seed)))
                throw new GameRuleException("bridge.closed","Build the Tsar Bridge before crossing the river.");
        }
        var nav=new RaidNavigation(village,pedestrian:true);var terrain=RegionalTerrain.For(village.Id,village.Wilds);
        var land=WorldMap.Land;
        var sx=(int)Math.Floor(from.X);var sy=(int)Math.Floor(from.Y);var tx=(int)Math.Floor(to.X);var ty=(int)Math.Floor(to.Y);
        if(!nav.Pass(tx,ty))throw NoPath();
        var start=RegionalTerrain.Index(sx,sy);var goal=RegionalTerrain.Index(tx,ty);
        var costs=new double[land.Width*land.Height];Array.Fill(costs,double.PositiveInfinity);
        var previous=new int[costs.Length];Array.Fill(previous,-1);
        var closed=new bool[costs.Length];var queue=new PriorityQueue<int,double>();costs[start]=0;queue.Enqueue(start,0);
        while(queue.TryDequeue(out var node,out _))
        {
            if(closed[node])continue;closed[node]=true;if(node==goal)break;
            var x=node%land.Width+land.X;var y=node/land.Width+land.Y;
            foreach(var (dx,dy) in new[]{(1,0),(-1,0),(0,1),(0,-1)})
            {
                var nx=x+dx;var ny=y+dy;if(!nav.Pass(nx,ny))continue;
                var next=RegionalTerrain.Index(nx,ny);var cost=costs[node]+(terrain[next]==AuthoredTerrain.Marsh?2.5:1);
                if(closed[next]||cost>=costs[next])continue;
                costs[next]=cost;previous[next]=node;queue.Enqueue(next,cost+Math.Abs(tx-nx)+Math.Abs(ty-ny));
            }
        }
        if(!closed[goal])throw NoPath();
        var nodes=new List<int>();for(var node=goal;node>=0;node=previous[node]){nodes.Add(node);if(node==start)break;}
        nodes.Reverse();var result=new List<TripWaypoint>();
        void Append(TripWaypoint next)
        {
            if(result.Count>=2)
            {
                var last=result[^1];var before=result[^2];
                if(last.Cost==next.Cost&&Math.Abs((last.X-before.X)*(next.Y-last.Y)-(last.Y-before.Y)*(next.X-last.X))<.000001)
                {result[^1]=next;return;}
            }
            result.Add(next);
        }
        foreach(var node in nodes)Append(new(){X=node%land.Width+land.X+.5,Y=node/land.Width+land.Y+.5,Cost=terrain[node]==AuthoredTerrain.Marsh?2.5:1});
        Append(new(){X=to.X,Y=to.Y,Cost=terrain[goal]==AuthoredTerrain.Marsh?2.5:1});
        return result;
    }
    private static GameRuleException NoPath()=>new("spot.noPath","There is no open path. Check water, bridges and blocked passes.");

    /// <summary>Apply a geometry change at its world time. Previously walked segments are never revalidated retroactively.</summary>
    public static bool Reconcile(Village village,Trip trip,DateTimeOffset changedAt,RaidNavigation? geometry=null)
    {
        if(trip.Waypoints.Count==0||trip.Waiting(changedAt)||trip.ArrivesAt<=changedAt)return false;
        var nav=geometry??new RaidNavigation(village,true);
        var previous=trip.From;var at=trip.SetOutAt;DateTimeOffset? blockedAt=null;string? reason=null;
        foreach(var step in trip.Waypoints)
        {
            var duration=previous.DistanceTo(step.Point)*step.Cost*Exploration.SecondsPerTile/Math.Max(.1,trip.SpeedMultiplier);
            var ends=at.AddSeconds(duration);
            if(ends>=changedAt&&duration>0)
            {
                var fromShare=Math.Clamp((changedAt-at).TotalSeconds/duration,0,1);
                var start=new TilePoint(previous.X+(step.X-previous.X)*fromShare,previous.Y+(step.Y-previous.Y)*fromShare);
                foreach(var (x,y,share) in Cells(start,step.Point))
                {
                    if(nav.Pass(x,y))continue;
                    var entered=at.AddSeconds(duration*(fromShare+(1-fromShare)*share));
                    // Stay on the approaching tile; if the bridge fell underneath, freeze at the actual current point.
                    var stop=share==0?entered:entered.AddMilliseconds(-1);
                    blockedAt=stop<changedAt?changedAt:stop;
                    reason=nav.Barrier(x,y)?.Gate==true?"gate":!nav.Dry(x,y)&&x is >=30 and <=33&&y is >=12 and <=13?"bridge":"path";
                    break;
                }
            }
            if(blockedAt is not null)break;previous=step.Point;at=ends;
        }
        var changed=trip.BlockedAt!=blockedAt||trip.BlockReason!=reason;
        trip.BlockedAt=blockedAt;trip.BlockReason=reason;trip.RouteEndsAt=trip.ArrivesAt;
        return changed;
    }

    private static IEnumerable<(int X,int Y,double Share)> Cells(TilePoint from,TilePoint to)
    {
        var x=(int)Math.Floor(from.X);var y=(int)Math.Floor(from.Y);var endX=(int)Math.Floor(to.X);var endY=(int)Math.Floor(to.Y);
        yield return(x,y,0);
        var dx=to.X-from.X;var dy=to.Y-from.Y;var sx=Math.Sign(dx);var sy=Math.Sign(dy);
        var tx=dx==0?double.PositiveInfinity:(sx>0?x+1-from.X:from.X-x)/Math.Abs(dx);
        var ty=dy==0?double.PositiveInfinity:(sy>0?y+1-from.Y:from.Y-y)/Math.Abs(dy);
        var dtx=dx==0?double.PositiveInfinity:1/Math.Abs(dx);var dty=dy==0?double.PositiveInfinity:1/Math.Abs(dy);
        for(var guard=0;(x!=endX||y!=endY)&&guard<WorldMap.Land.Width+WorldMap.Land.Height;guard++)
        {
            if(tx<ty){x+=sx;yield return(x,y,Math.Clamp(tx,0,1));tx+=dtx;}
            else if(ty<tx){y+=sy;yield return(x,y,Math.Clamp(ty,0,1));ty+=dty;}
            else
            {
                // A diagonal corner cannot slip past a newly closed wall.
                yield return(x+sx,y,Math.Clamp(tx,0,1));yield return(x,y+sy,Math.Clamp(tx,0,1));
                x+=sx;y+=sy;yield return(x,y,Math.Clamp(tx,0,1));tx+=dtx;ty+=dty;
            }
        }
    }

    public static Trip Resume(Village village,Trip trip,DateTimeOffset now)
    {
        if(!trip.Waiting(now))throw new GameRuleException("travel.notWaiting","This journey is not waiting at a blocked route.");
        var from=trip.PositionAt(now);var way=Find(village,from,trip.To);var resumed=Trip.Start(from,trip.To,now);
        resumed.Id=trip.Id;resumed.Waypoints=way;resumed.SpeedMultiplier=trip.SpeedMultiplier;resumed.RouteEndsAt=resumed.ArrivesAt;
        Exploration.Reveal(village.Explored,trip,now,village.Wilds?.Lookout);
        return resumed;
    }

    public static Trip Resume(Citizen citizen,Village village,DateTimeOffset now)
    {
        var previous=citizen.Trip??throw new GameRuleException("travel.changed","The journey changed.");
        var openWork=!citizen.CycleSettled&&citizen.CurrentTaskId is not null;
        var work=openWork&&citizen.CycleEndsAt is {} due?due-previous.ArrivesAt:TimeSpan.Zero;
        var resumed=Resume(village,previous,now);
        citizen.Trip=resumed;
        if(openWork)citizen.CycleEndsAt=resumed.ArrivesAt+(work>TimeSpan.Zero?work:TimeSpan.Zero);
        return resumed;
    }
}
