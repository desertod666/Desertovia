using Desertod.Domain.Combat;
using Desertod.Domain.Entities;

namespace Desertod.Domain.World;

/// <summary>Authoritative paths for journeys involving the expanded land. Legacy accepted trips keep their timing.</summary>
public static class RegionalTravel
{
    public static Trip SetOut(Village village,Trip? previous,TilePoint resting,TilePoint to,DateTimeOffset now)
    {
        if(!double.IsFinite(to.X)||!double.IsFinite(to.Y))throw new GameRuleException("spot.outsideLand","This spot is beyond the land.");
        BankAccess.Require(village,to);
        var from=previous?.PositionAt(now)??resting;
        if(!RegionalTerrain.NewLand(from)&&!RegionalTerrain.NewLand(to)&&previous?.Waypoints.Count is not >0)
            return Exploration.SetOut(village.Explored,previous,resting,to,now,village.Wilds?.Lookout);
        var way=Find(village,from,to);
        var trip=Trip.Start(from,to,now);trip.Waypoints=way;
        if(previous is not null)Exploration.Reveal(village.Explored,previous,now,village.Wilds?.Lookout);
        return trip;
    }

    public static List<TripWaypoint> Find(Village village,TilePoint from,TilePoint to)
    {
        BankAccess.Require(village,from);BankAccess.Require(village,to);
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
}
