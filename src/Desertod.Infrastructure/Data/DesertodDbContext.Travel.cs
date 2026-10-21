using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Desertod.Infrastructure.Data;

public sealed partial class DesertodDbContext
{
    public HashSet<Citizen> TravelChangedCitizens { get; }=[];
    public HashSet<Guid> TravelChangedVillages { get; }=[];

    private async Task ReconcileTravelAsync(TimeProvider clock,CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        var geometry=ChangeTracker.Entries().Where(GeometryChanged).ToArray();
        if(geometry.Length==0)return;
        foreach(var root in ChangeTracker.Entries<Village>().Where(e=>e.State is not (EntityState.Added or EntityState.Deleted)).ToArray())
        {
            var world=root.Entity;
            if(!geometry.Any(e=>e.Entity==world||e.Properties.Any(p=>p.Metadata.IsForeignKey()&&p.CurrentValue is Guid id&&id==world.Id)))continue;
            var at=clock.GetUtcNow();
            if(root.Property(v=>v.BridgeRuinedAt).IsModified&&world.BridgeRuinedAt is {} fell&&fell<at)at=fell;
            var people=await Citizens.Where(c=>c.VillageId==world.Id&&c.Trip!=null&&
                (c.Trip.BlockedAt!=null||c.Trip.RouteEndsAt>at||c.Trip.RouteEndsAt==null&&c.Trip.Waypoints.Count>0)).ToListAsync(ct);
            var nav=people.Count>0||world.TsarTrip?.Waypoints.Count>0?new RaidNavigation(world,true):null;
            foreach(var person in people)
            {
                if(person.Trip is not {} trip||trip.Waypoints.Count==0)continue;
                trip.RouteEndsAt??=trip.ArrivesAt;
                if(RegionalTravel.Reconcile(world,trip,at,nav))TravelChangedCitizens.Add(person);
            }
            if(world.TsarTrip is {} royal&&royal.Waypoints.Count>0)
            {
                royal.RouteEndsAt??=royal.ArrivesAt;
                if(RegionalTravel.Reconcile(world,royal,at,nav))TravelChangedVillages.Add(world.Id);
            }
            if(people.Any(p=>TravelChangedCitizens.Contains(p)))TravelChangedVillages.Add(world.Id);
        }
    }

    private static bool GeometryChanged(EntityEntry entry)
    {
        if(entry.State is EntityState.Unchanged or EntityState.Detached)return false;
        bool Changed(string name)=>entry.Properties.Any(p=>p.Metadata.Name==name&&p.IsModified&&!Equals(p.CurrentValue,p.OriginalValue));
        return entry.Entity switch
        {
            Village=>Changed(nameof(Village.BridgeOpened))||Changed(nameof(Village.BridgeRuinedAt)),
            Fortification wall=>entry.State is EntityState.Added or EntityState.Deleted||
                (entry.OriginalValues.GetValue<int>(nameof(wall.Damage))<100&&
                    (!entry.OriginalValues.GetValue<bool>(nameof(wall.Gate))||!entry.OriginalValues.GetValue<bool>(nameof(wall.Open))))!=wall.Blocks,
            ConstructionSite or Structure or StorageBuilding=>entry.State is EntityState.Added or EntityState.Deleted||Changed("PlotId")||Changed("Kind"),
            Obstacle=>entry.State is EntityState.Added or EntityState.Deleted||Changed(nameof(Obstacle.ClearedAt))||Changed(nameof(Obstacle.X))||Changed(nameof(Obstacle.Y)),
            _=>false,
        };
    }
}
