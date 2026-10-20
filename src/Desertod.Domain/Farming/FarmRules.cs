using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Domain.Farming;

/// <summary>
/// Beds on the village farms. A citizen sows a crop in an empty bed, it grows by itself, faster while the bed is wet,
/// and anyone can harvest it once it is ripe. Watering needs no skill; better seeds need more Endurance.
/// </summary>
public static class FarmRules
{
    private const double Epsilon = 1e-6;
    public static bool IsFarmWork(TaskDefinition task) => task.Effect is TaskEffect.Sow or TaskEffect.Water or TaskEffect.Harvest;

    public static List<FarmWorkTarget> PrepareTargets(Village village, TaskDefinition task, DateTimeOffset now,
        Guid? owner, FarmWorkTarget? requested, IReadOnlyList<FarmWorkClaim> work, Guid citizenId, string? workplacePlotId=null)
    {
        EnsureBeds(village);
        var beds= Beds(village,owner).Where(b=>workplacePlotId is null || b.PlotId==workplacePlotId).ToArray();
        FieldBed? picked=null;
        if(requested is not null)
        {
            picked=beds.SingleOrDefault(b=>b.PlotId==requested.PlotId && b.Index==requested.Index && b.GenerationId==requested.GenerationId)
                ?? throw new GameRuleException("farm.stale","This bed has changed. Open it again before starting work.");
        }
        Check(village,task,now,owner);
        var others=work.Where(c=>c.CitizenId!=citizenId && c.OwnerAccountId==owner).ToArray();
        bool Busy(FieldBed bed)=>others.Any(c=>c.Targets.Count==0 || c.Targets.Any(t=>t.PlotId==bed.PlotId && t.Index==bed.Index));
        bool Eligible(FieldBed bed)=>task.Effect switch {
            TaskEffect.Sow=>bed.Crop is null,
            TaskEffect.Water=>IsThirsty(village,bed,now),
            TaskEffect.Harvest=>IsRipe(village,bed,now),
            _=>false
        };
        if(picked is not null && !Eligible(picked))
            throw new GameRuleException("farm.changed","This action is not needed on the selected bed.");
        var selected=(picked is null ? (task.Effect==TaskEffect.Water ? beds.OrderBy(b=>b.WateredUntil??DateTimeOffset.MinValue) : beds.AsEnumerable()) : [picked])
            .Where(Eligible).Where(b=>!Busy(b)).Take(requested is null && task.Effect==TaskEffect.Water ? FarmCatalog.BedsPerWatering : 1).ToArray();
        if(selected.Length==0)throw new GameRuleException("farm.busy","The selected beds already have accepted work. Wait for it to finish.");
        return selected.Select(b=>new FarmWorkTarget{PlotId=b.PlotId,Index=b.Index,GenerationId=b.GenerationId}).ToList();
    }

    private static FieldBed? TargetBed(Village village, Guid? owner, FarmWorkTarget target) =>
        Beds(village,owner).SingleOrDefault(b=>b.PlotId==target.PlotId && b.Index==target.Index && b.GenerationId==target.GenerationId);

    /// <summary>Gives every farm its beds, also farms built before beds existed. Returns true if any bed was added.</summary>
    public static bool EnsureBeds(Village village)
    {
        var added = false;
        foreach (var farm in village.Structures.Where(s => s.Kind == BuildingKind.Farm))
        {
            for (var index = 0; index < FarmCatalog.BedsPerFarm; index++)
            {
                if (village.Fields.Any(b => b.PlotId == farm.PlotId && b.Index == index))
                {
                    continue;
                }

                village.Fields.Add(new FieldBed { PlotId = farm.PlotId, Index = index, GenerationId=Guid.CreateVersion7(), UpdatedAt = farm.BuiltAt });
                added = true;
            }
        }

        return added;
    }

    /// <summary>Rain waters every bed while it falls.</summary>
    public static double GrowthOf(Village village, FieldBed bed, DateTimeOffset now)
    {
        if (bed.Crop is not { } crop)
        {
            return 0;
        }

        var hours = Math.Max(0, (now - bed.UpdatedAt).TotalHours);
        var wetHours = Math.Min(hours, WeatherRules.WetHours(village, bed.UpdatedAt, now, bed.WateredUntil));
        var grown = (wetHours + (hours - wetHours) * FarmCatalog.DryGrowthRate) / FarmCatalog.Get(crop).GrowHours;
        return Math.Min(1, bed.Growth + grown);
    }

    public static bool IsRipe(Village village, FieldBed bed, DateTimeOffset now) => bed.Crop is not null && GrowthOf(village, bed, now) >= 1 - Epsilon;

    /// <summary>Bounds under the current growth rules, not a promise about future weather or care.
    /// Earliest assumes continuous moisture; latest assumes only the already applied water, then dry soil.</summary>
    public static (DateTimeOffset Earliest, DateTimeOffset Latest)? MaturityWindow(Village village,FieldBed bed,DateTimeOffset now)
    {
        if(bed.Crop is not {} crop)return null;
        var growth=GrowthOf(village,bed,now);
        var remaining=growth>=1-Epsilon ? 0 : (1-growth)*FarmCatalog.Get(crop).GrowHours;
        var guaranteedWet=Math.Min(remaining,Math.Max(0,((bed.WateredUntil??now)-now).TotalHours));
        return (now.AddHours(remaining),now.AddHours(guaranteedWet+(remaining-guaranteedWet)/FarmCatalog.DryGrowthRate));
    }

    public static bool IsWet(FieldBed bed, DateTimeOffset now) => bed.WateredUntil > now;

    /// <summary>A growing bed that dries out soon. Empty and ripe beds need no water, and none do in the rain.</summary>
    public static bool IsThirsty(Village village, FieldBed bed, DateTimeOffset now) =>
        bed.Crop is not null && !IsRipe(village, bed, now) && (bed.WateredUntil ?? DateTimeOffset.MinValue) - now < FarmCatalog.ThirstyBelow
        && !WeatherRules.IsRaining(village, now);

    public static FieldBed? EmptyBed(Village village, Guid? owner = null) => Beds(village, owner).FirstOrDefault(b => b.Crop is null);

    public static FieldBed? RipeBed(Village village, DateTimeOffset now, Guid? owner = null) => Beds(village, owner).FirstOrDefault(b => IsRipe(village, b, now));

    /// <summary>Thirsty beds, driest first.</summary>
    public static IEnumerable<FieldBed> ThirstyBeds(Village village, DateTimeOffset now, Guid? owner = null) =>
        Beds(village, owner).Where(b => IsThirsty(village, b, now)).OrderBy(b => b.WateredUntil ?? DateTimeOffset.MinValue);

    /// <summary>Checks that the work has a bed to do it on. Throws a rule error explaining why not.</summary>
    public static void Check(Village village, TaskDefinition task, DateTimeOffset now, Guid? owner = null)
    {
        EnsureBeds(village);
        switch (task.Effect)
        {
            case TaskEffect.Sow when EmptyBed(village, owner) is null:
                throw new GameRuleException("farm.noEmptyBed", "Every bed is sown. Harvest a ripe one first.");
            case TaskEffect.Water when WeatherRules.IsRaining(village, now):
                throw new GameRuleException("farm.raining", "It is raining: the rain waters the beds.");
            case TaskEffect.Water when !ThirstyBeds(village, now, owner).Any():
                throw new GameRuleException("farm.nothingToWater", "No bed needs water now.");
            case TaskEffect.Harvest when RipeBed(village, now, owner) is null:
                throw new GameRuleException("farm.nothingRipe", "Nothing is ripe yet.");
        }
    }

    /// <summary>Sows the crop in the first empty bed. Returns false when someone sowed the last one first.</summary>
    public static bool Sow(Village village, CropKind crop, DateTimeOffset now, Guid? owner = null, FarmWorkTarget? target = null,
        FarmWorker? worker=null,int contributionVersion=0,ICollection<FarmCropSnapshot>? changes=null)
    {
        EnsureBeds(village);
        if ((target is null ? EmptyBed(village, owner) : TargetBed(village,owner,target)) is not { Crop:null } bed)
        {
            return false;
        }

        bed.Crop = crop;
        bed.GenerationId=Guid.CreateVersion7();
        bed.Growth = 0;
        bed.UpdatedAt = now;
        bed.ContributionVersion=owner is null && worker is not null ? contributionVersion : 0;
        bed.SownAt=now;
        bed.Contributions.Clear();
        if(worker is not null)FarmContributionRules.Record(bed,FarmStep.Sow,worker,now);
        if(FarmContributionRules.Snapshot(bed) is {} snapshot)changes?.Add(snapshot);
        return true;
    }

    /// <summary>Waters the thirstiest beds. Returns how many were watered.</summary>
    public static int Water(Village village, DateTimeOffset now, Guid? owner = null, IReadOnlyList<FarmWorkTarget>? targets = null,
        FarmWorker? worker=null,ICollection<FarmCropSnapshot>? changes=null,DateTimeOffset? completedAt=null)
    {
        EnsureBeds(village);
        DateTimeOffset At(FieldBed bed)=>owner is null && worker is not null && bed.ContributionVersion==FarmContributionRules.Version ? completedAt??now : now;
        var watered = (targets is null ? ThirstyBeds(village, now, owner).Take(FarmCatalog.BedsPerWatering)
            : targets.Select(t=>TargetBed(village,owner,t)).OfType<FieldBed>().Distinct().Where(b=>IsThirsty(village,b,At(b)))).ToList();
        foreach (var bed in watered)
        {
            var at=At(bed);
            bed.Growth = GrowthOf(village, bed, at);
            bed.UpdatedAt = at;
            bed.WateredUntil = at + FarmCatalog.WaterDuration;
            if(owner is null && worker is not null)
            {
                FarmContributionRules.Record(bed,FarmStep.Water,worker,at);
                if(FarmContributionRules.Snapshot(bed) is {} snapshot)changes?.Add(snapshot);
            }
        }

        return watered.Count;
    }

    /// <summary>Empties the first ripe bed and returns its crop; null when someone harvested it first.</summary>
    public static CropDefinition? Harvest(Village village, DateTimeOffset now, Guid? owner = null, FarmWorkTarget? target = null,
        FarmWorker? worker=null,ICollection<FarmCropSnapshot>? changes=null)
    {
        EnsureBeds(village);
        if ((target is null ? RipeBed(village, now, owner) : TargetBed(village,owner,target)) is not { Crop: { } crop } bed || !IsRipe(village,bed,now))
        {
            return null;
        }

        if(owner is null && worker is not null)
        {
            FarmContributionRules.Record(bed,FarmStep.Harvest,worker,now);
            if(FarmContributionRules.Snapshot(bed,now) is {} snapshot)changes?.Add(snapshot);
        }
        bed.Crop = null;
        bed.GenerationId=Guid.CreateVersion7();
        bed.Growth = 0;
        bed.UpdatedAt = now;
        bed.SownAt=null;
        bed.Contributions.Clear();
        bed.ContributionVersion=0;
        return FarmCatalog.Get(crop);
    }

    /// <summary>
    /// Walking time of farm work: water is carried from the river to the beds, the harvest from the beds to storage.
    /// Null for other tasks.
    /// </summary>
    public static TimeSpan? Walk(Village village, TaskDefinition task, DateTimeOffset now, Guid? owner = null, IReadOnlyList<FarmWorkTarget>? targets = null)
    {
        switch (task.Effect)
        {
            case TaskEffect.Sow:
                return TimeSpan.Zero;
            case TaskEffect.Water:
            {
                var bed = targets?.FirstOrDefault() is {} waterTarget ? TargetBed(village,owner,waterTarget) : ThirstyBeds(village, now, owner).FirstOrDefault();
                return bed is null ? TimeSpan.Zero : WorldMap.Walk(WorldMap.Places[WorldMap.Ford], WorldMap.Yard(bed.PlotId));
            }

            case TaskEffect.Harvest:
            {
                if ((targets?.FirstOrDefault() is {} harvestTarget ? TargetBed(village,owner,harvestTarget) : RipeBed(village, now, owner)) is not { Crop: { } crop } bed)
                {
                    return TimeSpan.Zero;
                }

                var yard = WorldMap.Yard(bed.PlotId);
                return WorldMap.Walk(yard, village.DropPoint(FarmCatalog.Get(crop).Harvest[0].Resource, yard));
            }

            default:
                return null;
        }
    }

    private static IEnumerable<FieldBed> Beds(Village village, Guid? owner = null) => village.Fields.Where(b=>village.Structures.Any(s=>s.PlotId==b.PlotId && s.Kind==BuildingKind.Farm && s.OwnerAccountId==owner && !s.Decommissioning && s.RuinedAt is null && s.Damage<100)).OrderBy(b => b.PlotId).ThenBy(b => b.Index);
}
