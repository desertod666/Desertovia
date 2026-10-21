using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.World;

namespace Desertod.Domain.Dungeons;

public enum HeroState
{
    Fighting,

    /// <summary>Fell: runs for the entrance with a skull over the head and takes no part any more.</summary>
    Fleeing,

    /// <summary>Out of the dungeon.</summary>
    Left,
}

/// <summary>A hero as they enter the dungeon map.</summary>
/// <param name="Id">The citizen, or the Tsar's account.</param>
/// <param name="Protection">Share of each blow the armor stops.</param>
/// <param name="Arrows">Shots of an archer; null when the hero does not shoot.</param>
/// <param name="Invulnerable">Local development only: blows land, but health stays full and the clock does not end the run.</param>
public sealed record DungeonFighter(
    Guid Id,
    Guid AccountId,
    string Name,
    HeroClass Class,
    double Health,
    double Damage,
    double Protection,
    int? Arrows,
    bool CanHeal,
    bool Invulnerable = false)
{
    public HeroClass? CombatClass { get; init; }
    public HeroClassDefinition? CombatDefinition { get; init; }
}

public enum DungeonEventKind
{
    /// <summary>A hero struck a foe.</summary>
    Hit,

    /// <summary>A foe struck a hero.</summary>
    Struck,

    Healed,
    Slain,
    KnockedOut,
    Left,
}

/// <param name="Ranged">An arrow or a bolt flew; the client draws it.</param>
public sealed record DungeonEvent(DungeonEventKind Kind, Guid? HeroId, int? FoeId, double Amount = 0, bool Ranged = false);

public sealed class SimulatedHero
{
    public required DungeonFighter Fighter { get; init; }
    public Guid Id => Fighter.Id;
    public double X { get; set; }
    public double Y { get; set; }
    public double Health { get; set; }
    public HeroState State { get; set; }
    public int? TargetFoe { get; set; }
    public Guid? HealTarget { get; set; }
    public TilePoint? Destination { get; set; }
    public int? Arrows { get; set; }
    public int Kills { get; set; }
    public int Experience { get; set; }
    public Dictionary<ResourceKind, int> Loot { get; } = [];
    public HashSet<int> FoesHit { get; } = [];
    public bool Healed { get; set; }
    public EnemyKind? Killer { get; set; }
    public bool ExternallyWithdrawn { get; set; }
    public bool RetreatRequested { get; set; }

    internal double Cooldown { get; set; }
    internal PathFollower Path { get; } = new();

    public bool IsTsar => Fighter.Class == HeroClass.Voivode;
    public HeroClassDefinition Definition => Fighter.CombatDefinition??HeroClassCatalog.Get(Fighter.CombatClass??Fighter.Class);

    /// <summary>An archer out of arrows fights hand to hand.</summary>
    public double Range => Arrows is 0 ? HeroClassCatalog.Get(HeroClass.Brawler).Range : Definition.Range;

    public bool Shoots => Arrows is > 0;
}

public sealed class SimulatedFoe
{
    public int Id { get; init; }
    public required DungeonFoe Definition { get; init; }
    public EnemyKind Kind => Definition.Kind;
    public double X { get; set; }
    public double Y { get; set; }
    public double Health { get; set; }
    public Guid? Target { get; set; }
    public bool IsAlive => Health > 0;

    internal double Cooldown { get; set; }
    internal PathFollower Path { get; } = new();
}

/// <summary>
/// The fight on a dungeon map, run in memory by the server. Heroes only attack the foe their player picked; a foe
/// chases the nearest hero who comes within its aggro range, or anyone who strikes it, and never lets go. Each slain
/// foe rewards every hero still fighting. Pure and deterministic: the same intents and seed play out the same.
/// </summary>
public sealed partial class DungeonSimulation
{
    /// <summary>A walker this close to a waypoint has reached it.</summary>
    private const double Reach = 0.05;

    /// <summary>A fleeing hero this close to the entrance is out.</summary>
    private const double ExitReach = 0.5;

    /// <summary>An archer out of arrows hits with the bow's limb: this share of a shot.</summary>
    private const double Brawling = 0.5;

    private readonly List<SimulatedHero> heroes;
    private readonly List<SimulatedFoe> foes = [];
    private readonly IReadOnlyList<DungeonDefinition> floors;
    private readonly TimeSpan timeLimit;
    private readonly DungeonDefinition definition;
    private int slainBefore;
    private int floor = -1;

    public DungeonSimulation(DungeonDefinition dungeon, IEnumerable<DungeonFighter> fighters)
    {
        definition = dungeon;
        floors = dungeon.Floors.Count > 0 ? dungeon.Floors : [dungeon];
        timeLimit = dungeon.TimeLimit;
        heroes = fighters.Select(fighter => new SimulatedHero
        {
            Fighter = fighter,
            Health = fighter.Health,
            Arrows = fighter.Arrows,
        }).ToList();
        EnterFloor(0);
    }

    public DungeonDefinition Dungeon { get; private set; } = null!;

    /// <summary>Zero on the first floor.</summary>
    public int Floor => floor;

    public int FloorCount => floors.Count;
    public IReadOnlyList<SimulatedHero> Heroes => heroes;
    public IReadOnlyList<SimulatedFoe> Foes => foes;

    /// <summary>Seconds since the fight started.</summary>
    public double Elapsed { get; private set; }

    public DungeonOutcome Outcome { get; private set; } = DungeonOutcome.Pending;

    /// <summary>The time ran out: nobody still fighting is wounded.</summary>
    public bool TimedOut { get; private set; }

    public int Slain => slainBefore + foes.Count(f => !f.IsAlive);

    public bool IsOver => Outcome != DungeonOutcome.Pending;

    public SimulatedHero? Hero(Guid id) => heroes.FirstOrDefault(h => h.Id == id);

    /// <summary>Walk to a spot; drops the target.</summary>
    public bool MoveTo(Guid heroId, double x, double y)
    {
        if (Fighting(heroId) is not { RetreatRequested:false } hero || !double.IsFinite(x)||!double.IsFinite(y)||!Dungeon.IsWalkable((int)Math.Floor(x), (int)Math.Floor(y)))
        {
            return false;
        }

        hero.TargetFoe = null;
        hero.HealTarget = null;
        hero.Destination = new TilePoint(x, y);
        hero.Path.Clear();
        return true;
    }

    /// <summary>Pick a foe: the hero walks up to it and strikes until it falls.</summary>
    public bool Attack(Guid heroId, int foeId)
    {
        if (Fighting(heroId) is not { RetreatRequested:false } hero || foes.ElementAtOrDefault(foeId) is not { IsAlive: true })
        {
            return false;
        }

        hero.TargetFoe = foeId;
        hero.HealTarget = null;
        hero.Destination = null;
        hero.Path.Clear();
        return true;
    }

    /// <summary>A healer tends an ally (or themselves) while the ally is hurt.</summary>
    public bool Heal(Guid healerId, Guid heroId)
    {
        if (Fighting(healerId) is not { Fighter.CanHeal: true,RetreatRequested:false } healer || Fighting(heroId) is null)
        {
            return false;
        }

        healer.HealTarget = heroId;
        healer.TargetFoe = null;
        healer.Destination = null;
        healer.Path.Clear();
        return true;
    }

    public void WithdrawToHospital(Guid heroId)
    {
        if (Hero(heroId) is not { State: HeroState.Fighting } hero) return;
        hero.ExternallyWithdrawn = true;
        hero.State = HeroState.Left;
        hero.Destination = null;
        hero.TargetFoe = null;
        hero.HealTarget = null;
    }

    /// <summary>A voluntary retreat walks to the entrance while the hero remains a valid enemy target.</summary>
    public bool Retreat(Guid heroId)
    {
        if(IsOver||Fighting(heroId) is not {} hero)return false;
        if(hero.RetreatRequested)return true;
        hero.RetreatRequested=true;hero.Destination=null;hero.TargetFoe=null;hero.HealTarget=null;hero.Path.Clear();
        return true;
    }

    /// <summary>Advances the fight; returns what happened, for the clients to show.</summary>
    public IReadOnlyList<DungeonEvent> Tick(double seconds, Random random)
    {
        var events = new List<DungeonEvent>();
        if (IsOver || seconds <= 0)
        {
            return events;
        }

        Elapsed += seconds;
        foreach (var hero in heroes)
        {
            hero.Cooldown = Math.Max(0, hero.Cooldown - seconds);
            switch (hero.State)
            {
                case HeroState.Fighting:
                    ActHero(hero, seconds, random, events);
                    break;
                case HeroState.Fleeing:
                    Flee(hero, seconds, events);
                    break;
            }
        }

        var beforeFoes=foes.Where(f=>f.IsAlive).ToDictionary(f=>f.Id,f=>new TilePoint(f.X,f.Y));
        foreach (var foe in foes.Where(f => f.IsAlive))
        {
            foe.Cooldown = Math.Max(0, foe.Cooldown - seconds);
            ActFoe(foe, seconds, events);
        }

        MoveCrowd(beforeFoes,seconds);

        if (foes.Count > 0 && foes.All(f => !f.IsAlive))
        {
            if (floor < floors.Count - 1 && heroes.Any(h => h.State == HeroState.Fighting))
            {
                EnterFloor(floor + 1);
            }
            else
            {
                Outcome = floor < floors.Count - 1 ? DungeonOutcome.Defeat : DungeonOutcome.Victory;
            }
        }
        else if (heroes.All(h => h.State != HeroState.Fighting))
        {
            Outcome = DungeonOutcome.Defeat;
        }
        else if (Elapsed >= timeLimit.TotalSeconds && heroes.Any(h => h.State == HeroState.Fighting && !h.Fighter.Invulnerable))
        {
            Outcome = DungeonOutcome.Defeat;
            TimedOut = true;
        }

        return events;
    }

    /// <summary>What the hero takes home; see <see cref="DungeonRules.Settle"/>.</summary>
    public DungeonHeroOutcome OutcomeOf(SimulatedHero hero) =>
        new(
            hero.Kills,
            hero.Experience,
            hero.Loot.Select(l => new ResourceAmount(l.Key, l.Value)).ToList(),
            hero.FoesHit.Count,
            hero.Arrows ?? 0,
            hero.Healed,
            !hero.ExternallyWithdrawn && hero.State != HeroState.Fighting,
            hero.Killer);

    private SimulatedHero? Fighting(Guid heroId) => heroes.FirstOrDefault(h => h.Id == heroId && h.State == HeroState.Fighting);

    private void ActHero(SimulatedHero hero, double seconds, Random random, List<DungeonEvent> events)
    {
        var speed = HeroClassCatalog.HeroSpeed * seconds;
        if(hero.RetreatRequested)
        {
            var exit=Dungeon.Entrance;
            Walk(hero.Path,exit.X,exit.Y,speed,p=>(hero.X,hero.Y)=p,(hero.X,hero.Y));
            if(Distance(hero.X,hero.Y,exit.X,exit.Y)<=ExitReach)
            {
                hero.State=HeroState.Left;hero.ExternallyWithdrawn=true;
                events.Add(new(DungeonEventKind.Left,hero.Id,null));
            }
            return;
        }
        if (hero.HealTarget is { } allyId)
        {
            if (Fighting(allyId) is not { } ally)
            {
                hero.HealTarget = null;
                return;
            }

            if (Distance(hero.X, hero.Y, ally.X, ally.Y) > hero.Definition.Range)
            {
                Walk(hero.Path, ally.X, ally.Y, speed, p => (hero.X, hero.Y) = p, (hero.X, hero.Y));
                return;
            }

            hero.Path.Clear();
            if (hero.Cooldown <= 0 && ally.Health < ally.Fighter.Health)
            {
                var healed = Math.Min(hero.Definition.Heal, ally.Fighter.Health - ally.Health);
                ally.Health += healed;
                hero.Healed = true;
                hero.Cooldown = hero.Definition.AttackSeconds;
                events.Add(new DungeonEvent(DungeonEventKind.Healed, ally.Id, null, healed));
            }

            return;
        }

        if (hero.TargetFoe is { } foeId)
        {
            var foe = foes[foeId];
            if (!foe.IsAlive)
            {
                hero.TargetFoe = null;
                return;
            }

            if (Distance(hero.X, hero.Y, foe.X, foe.Y) > hero.Range)
            {
                Walk(hero.Path, foe.X, foe.Y, speed, p => (hero.X, hero.Y) = p, (hero.X, hero.Y));
                return;
            }

            hero.Path.Clear();
            if (hero.Cooldown <= 0)
            {
                Strike(hero, foe, random, events);
            }

            return;
        }

        if (hero.Destination is { } destination)
        {
            if (Walk(hero.Path, destination.X, destination.Y, speed, p => (hero.X, hero.Y) = p, (hero.X, hero.Y)))
            {
                hero.Destination = null;
            }
        }
    }

    private void Strike(SimulatedHero hero, SimulatedFoe foe, Random random, List<DungeonEvent> events)
    {
        var ranged = hero.Shoots;
        var damage = hero.Fighter.Damage;
        if (hero.Arrows is > 0)
        {
            hero.Arrows--;
        }
        else if (hero.Arrows is 0)
        {
            damage *= Brawling;
        }

        foe.Health -= damage;
        foe.Target ??= hero.Id;
        hero.FoesHit.Add(foe.Id);
        hero.Cooldown = hero.Definition.AttackSeconds;
        events.Add(new DungeonEvent(DungeonEventKind.Hit, hero.Id, foe.Id, damage, ranged));
        if (!foe.IsAlive)
        {
            Slay(foe, hero, random, events);
        }
    }

    private void Slay(SimulatedFoe foe, SimulatedHero killer, Random random, List<DungeonEvent> events)
    {
        foe.Health = 0;
        foe.Target = null;
        killer.Kills++;
        events.Add(new DungeonEvent(DungeonEventKind.Slain, killer.Id, foe.Id));
        foreach (var hero in heroes.Where(h => h.State == HeroState.Fighting && (!h.IsTsar||h.Fighter.CombatClass is not null)))
        {
            hero.Experience += foe.Definition.Experience;
            // Keep existing group loot allocation until its separate owner decision.
            if(hero.IsTsar)continue;
            foreach (var loot in foe.Definition.Loot)
            {
                if (random.NextDouble() >= loot.Chance)
                {
                    continue;
                }

                var amount = random.Next(loot.Minimum, loot.Maximum + 1);
                hero.Loot[loot.Resource] = hero.Loot.GetValueOrDefault(loot.Resource) + amount;
            }
        }

        foreach (var hero in heroes.Where(h => h.TargetFoe == foe.Id))
        {
            hero.TargetFoe = null;
        }
    }

    private void Flee(SimulatedHero hero, double seconds, List<DungeonEvent> events)
    {
        var exit = Dungeon.Entrance;
        Walk(hero.Path, exit.X, exit.Y, HeroClassCatalog.FleeSpeed * seconds, p => (hero.X, hero.Y) = p, (hero.X, hero.Y));
        if (Distance(hero.X, hero.Y, exit.X, exit.Y) <= ExitReach)
        {
            hero.State = HeroState.Left;
            events.Add(new DungeonEvent(DungeonEventKind.Left, hero.Id, null));
        }
    }

    private void ActFoe(SimulatedFoe foe, double seconds, List<DungeonEvent> events)
    {
        if (foe.Target is { } current && Fighting(current) is null)
        {
            foe.Target = null;
            foe.Path.Clear();
        }

        var profile=MonsterBrain.Profile(EnemyCatalog.Get(foe.Kind),foe.Kind==EnemyKind.BarrowKing?"boss":"regular",foe.Id);
        foe.Target ??= Guid.TryParse(MonsterBrain.Choose(profile,new(foe.X,foe.Y),null,heroes
            .Where(h=>h.State==HeroState.Fighting&&Distance(h.X,h.Y,foe.X,foe.Y)<=foe.Definition.AggroRange)
            .Select(h=>new MonsterObjective("hero",h.Id.ToString(),new(h.X,h.Y),1,
                foes.Count(other=>other.Id!=foe.Id&&other.Target==h.Id))))?.Id,out var selected)?selected:null;
        if (foe.Target is not { } targetId)
        {
            return;
        }

        var target = Fighting(targetId)!;
        if (Distance(foe.X, foe.Y, target.X, target.Y) > foe.Definition.AttackRange)
        {
            Walk(foe.Path, target.X, target.Y, foe.Definition.Speed * seconds, p => (foe.X, foe.Y) = p, (foe.X, foe.Y));
            return;
        }

        foe.Path.Clear();
        if (foe.Cooldown > 0)
        {
            return;
        }

        if (target.Fighter.Invulnerable)
        {
            foe.Cooldown = foe.Definition.AttackSeconds;
            return;
        }

        var damage = foe.Definition.Damage * (1 - target.Fighter.Protection);
        target.Health -= damage;
        foe.Cooldown = foe.Definition.AttackSeconds;
        events.Add(new DungeonEvent(DungeonEventKind.Struck, target.Id, foe.Id, damage, foe.Definition.AttackRange > 1.5));
        if (target.Health <= 0)
        {
            KnockOut(target, foe, events);
        }
    }

    private void MoveCrowd(IReadOnlyDictionary<int,TilePoint> before,double seconds)
    {
        var alive=foes.Where(f=>f.IsAlive).ToArray();
        var actors=alive.Select(f=>new CrowdActor(f.Id,before[f.Id],new(f.X,f.Y),
            before[f.Id].DistanceTo(new(f.X,f.Y))>.001?f.Definition.Speed:0,f.Kind==EnemyKind.BarrowKing?.3:.2)).ToList();
        var blocker=-1;
        foreach(var h in heroes.Where(h=>h.State==HeroState.Fighting))actors.Add(new(blocker--,new(h.X,h.Y),new(h.X,h.Y),0));
        var positions=MonsterCrowd.Move(actors,seconds,(from,to)=>MonsterCrowd.CanTraverse(from,to,Dungeon.IsWalkable));
        foreach(var foe in alive)
        {
            var next=positions[foe.Id];
            if(next.DistanceTo(new(foe.X,foe.Y))>.05)foe.Path.Clear();
            foe.X=next.X;foe.Y=next.Y;
        }
    }

    private void KnockOut(SimulatedHero hero, SimulatedFoe foe, List<DungeonEvent> events)
    {
        hero.Health = 0;
        hero.State = HeroState.Fleeing;
        hero.Killer = foe.Kind;
        hero.TargetFoe = null;
        hero.HealTarget = null;
        hero.Destination = null;
        hero.Path.Clear();
        events.Add(new DungeonEvent(DungeonEventKind.KnockedOut, hero.Id, foe.Id));
        foreach (var chaser in foes.Where(f => f.Target == hero.Id))
        {
            chaser.Target = null;
            chaser.Path.Clear();
        }
    }

    /// <summary>Moves a walker along the grid toward a point. Returns true once there.</summary>
    private bool Walk(PathFollower path, double toX, double toY, double distance, Action<(double X, double Y)> move, (double X, double Y) from)
    {
        var goal = ((int)Math.Floor(toX), (int)Math.Floor(toY));
        if (path.Goal != goal || path.Waypoints.Count == 0 && Distance(from.X, from.Y, toX, toY) > Reach)
        {
            path.Goal = goal;
            path.Waypoints.Clear();
            var start = ((int)Math.Floor(from.X), (int)Math.Floor(from.Y));
            if (DungeonPathfinder.Find(Dungeon, start, goal) is { } tiles)
            {
                foreach (var tile in tiles.Skip(1))
                {
                    path.Waypoints.Enqueue(new TilePoint(tile.X + 0.5, tile.Y + 0.5));
                }

                if (path.Waypoints.Count > 0)
                {
                    path.ReplaceLast(new TilePoint(toX, toY));
                }
                else
                {
                    path.Waypoints.Enqueue(new TilePoint(toX, toY));
                }
            }
        }

        var (x, y) = from;
        while (distance > 0 && path.Waypoints.TryPeek(out var next))
        {
            var gap = Distance(x, y, next.X, next.Y);
            if (gap <= distance + Reach)
            {
                (x, y) = (next.X, next.Y);
                distance -= gap;
                path.Waypoints.Dequeue();
                continue;
            }

            x += (next.X - x) / gap * distance;
            y += (next.Y - y) / gap * distance;
            distance = 0;
        }

        move((x, y));
        return path.Waypoints.Count == 0 && Distance(x, y, toX, toY) <= Reach + 1e-9;
    }

    private static double Distance(double x1, double y1, double x2, double y2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));

    /// <summary>
    /// Opens a floor. Health, arrows and loot stay. A hero who already fell is sent out and does not come along.
    /// </summary>
    private void EnterFloor(int index)
    {
        if (foes.Count > 0)
        {
            slainBefore += foes.Count(f => !f.IsAlive);
        }

        floor = index;
        Dungeon = floors[index];
        foes.Clear();
        for (var spawnIndex = 0; spawnIndex < Dungeon.Spawns.Count; spawnIndex++)
        {
            var spawn = Dungeon.Spawns[spawnIndex];
            var definition = Dungeon.FoeDefinitions.TryGetValue(spawn.Kind,out var local)?local:DungeonCatalog.Foe(spawn.Kind);
            foes.Add(new SimulatedFoe
            {
                Id = spawnIndex,
                Definition = definition,
                X = spawn.X + 0.5,
                Y = spawn.Y + 0.5,
                Health = definition.Health,
            });
        }

        var file = 0;
        foreach (var hero in heroes)
        {
            hero.TargetFoe = null;
            hero.HealTarget = null;
            hero.Destination = null;
            hero.Path.Clear();
            hero.Cooldown = 0;
            if (hero.State == HeroState.Fighting && hero.Health > 0)
            {
                var start = StartTile(Dungeon, file++);
                hero.X = start.X;
                hero.Y = start.Y;
            }
            else
            {
                if (hero.Health <= 0 || hero.State == HeroState.Fleeing)
                {
                    hero.Health = 0;
                }

                hero.State = HeroState.Left;
            }
        }
    }

    /// <summary>Heroes come in by the entrance in a few short files, so they do not stand on each other.</summary>
    private static TilePoint StartTile(DungeonDefinition dungeon, int index)
    {
        int[] offsets = [0, -1, 1, -2, 2, -3, 3];
        var x = 1 + index / offsets.Length;
        var y = (int)Math.Floor(dungeon.Entrance.Y) + offsets[index % offsets.Length];
        return dungeon.IsWalkable(x, y) ? new TilePoint(x + 0.5, y + 0.5) : new TilePoint(1.5, dungeon.Entrance.Y);
    }
}

internal sealed class PathFollower
{
    public (int X, int Y)? Goal { get; set; }
    public Queue<TilePoint> Waypoints { get; } = new();

    public void Clear()
    {
        Goal = null;
        Waypoints.Clear();
    }

    /// <summary>Ends the path at the exact point instead of the centre of its tile.</summary>
    public void ReplaceLast(TilePoint point)
    {
        var items = Waypoints.ToList();
        items[^1] = point;
        Waypoints.Clear();
        foreach (var item in items)
        {
            Waypoints.Enqueue(item);
        }
    }
}

/// <summary>A* over the dungeon tiles, in eight directions; a diagonal step may not cut a blocked corner.</summary>
public static class DungeonPathfinder
{
    public static IReadOnlyList<(int X, int Y)>? Find(DungeonDefinition dungeon, (int X, int Y) start, (int X, int Y) goal)
    {
        if (!dungeon.Inside(goal.X, goal.Y) || !dungeon.Inside(start.X, start.Y))
        {
            return null;
        }

        if (!dungeon.IsWalkable(goal.X, goal.Y))
        {
            goal = NearestWalkable(dungeon, goal) ?? start;
        }

        var open = new PriorityQueue<(int X, int Y), double>();
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
        var cost = new Dictionary<(int X, int Y), double> { [start] = 0 };
        open.Enqueue(start, Heuristic(start, goal));
        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal)
            {
                var path = new List<(int X, int Y)> { current };
                while (cameFrom.TryGetValue(current, out var previous))
                {
                    current = previous;
                    path.Add(current);
                }

                path.Reverse();
                return path;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    var next = (X: current.X + dx, Y: current.Y + dy);
                    if (!dungeon.IsWalkable(next.X, next.Y)
                        || dx != 0 && dy != 0 && (!dungeon.IsWalkable(current.X + dx, current.Y) || !dungeon.IsWalkable(current.X, current.Y + dy)))
                    {
                        continue;
                    }

                    var step = cost[current] + (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1);
                    if (cost.TryGetValue(next, out var known) && known <= step)
                    {
                        continue;
                    }

                    cost[next] = step;
                    cameFrom[next] = current;
                    open.Enqueue(next, step + Heuristic(next, goal));
                }
            }
        }

        return null;
    }

    private static double Heuristic((int X, int Y) a, (int X, int Y) b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) + (Math.Sqrt(2) - 1) * Math.Min(dx, dy);
    }

    private static (int X, int Y)? NearestWalkable(DungeonDefinition dungeon, (int X, int Y) tile)
    {
        for (var radius = 1; radius < Math.Max(dungeon.Width, dungeon.Height); radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (dungeon.IsWalkable(tile.X + dx, tile.Y + dy))
                    {
                        return (tile.X + dx, tile.Y + dy);
                    }
                }
            }
        }

        return null;
    }
}
