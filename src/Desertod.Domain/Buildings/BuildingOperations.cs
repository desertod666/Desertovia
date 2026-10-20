using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
namespace Desertod.Domain.Buildings;

public sealed class CivicOrder
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string PlotId { get; set; }
    public required string Operation { get; set; }
    public BuildingKind? BuildingKind { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid? PartnerAccountId { get; set; }
    public bool PartnerAccepted { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string NoticeState { get; set; } = "Queued";
    public DateTimeOffset? NoticeAt { get; set; }
}

public static class BuildingOperations
{
    public static BuildingDefinition Definition(ConstructionSite site)
    {
        var original=BuildingCatalog.Get(site.Kind);
        StageJob Haul(string id,int amount)=>new(id,amount);
        if(site.Operation==FortificationUpgrades.Operation)return original with {Stages=[new("reinforce","Fortification reinforcement",3,
            site.TargetLevel==2?[Haul("bring-logs",240),Haul("bring-planks",80),new("raise-posts",site.OperationWork)]
                :[Haul("bring-stones",400),Haul("bring-logs",80),Haul("bring-planks",80),new("raise-posts",site.OperationWork)])]};
        if(site.Operation=="castle-upgrade") return original with { Stages = original.Stages.Select(stage => stage with {
            Jobs = stage.Jobs.Select(job => BuildingCatalog.FindJob(job.JobId)?.Kind == JobKind.Labor
                ? job with { Amount = checked(job.Amount * (1 << (site.TargetLevel - 1))) } : job).ToArray() }).ToArray() };
        if(site.Operation is "demolish" or "site-demolish") return original with { Stages=[new("dismantle","Dismantling",1,[new("dismantle",site.OperationWork)])] };
        if(site.Operation=="storage-upgrade") return original with { Stages=[new("upgrade","Storage upgrade",3,
            [Haul("bring-logs",60),Haul("bring-stones",40),Haul("bring-planks",20),new("raise-posts",24)])] };
        if(site.Operation=="housing-upgrade")
        {
            var last=site.TargetLevel==3;
            return original with { Stages=[new("expand","Housing expansion",3,
                [Haul("bring-logs",last?160:100),Haul("bring-stones",last?120:80),Haul("bring-planks",last?60:30),
                 Haul("bring-reeds",last?60:40),new("raise-posts",last?80:60)])] };
        }
        return original;
    }

    public static CivicOrder RequestBuild(Village village, Guid actor, BuildingKind kind, int x, int y, DateTimeOffset now)
    {
        ConstructionRules.ValidatePlacement(village, kind, x, y, checkCapacity: false);
        var plot = WorldMap.PlotAt(x, y);
        if (village.CivicOrders.Any(o => o.PlotId == plot && o.Status is "pending" or "working"))
            throw new GameRuleException("building.orderExists", "An application already exists for this plot.");
        RequireRequestCapacity(village, null);
        var order = new CivicOrder { PlotId = plot, Operation = "build", BuildingKind = kind, RequestedBy = actor, CreatedAt = now };
        village.CivicOrders.Add(order);
        return order;
    }

    public static CivicOrder Request(Village village,Guid actor,string plot,string operation,DateTimeOffset now)
    {
        if(plot==FortificationUpgrades.Plot&&operation=="upgrade")return FortificationUpgrades.Request(village,actor,now);
        if(operation=="upgrade"&&village.DefeatedAt is not null)throw new GameRuleException("village.defeated","Restore the royal castle first.");
        if(operation is not ("demolish" or "upgrade")) throw new GameRuleException("building.operation","Unknown building action.");
        if(village.CivicOrders.Any(o=>o.PlotId==plot&&o.Status is "pending" or "working"))
            throw new GameRuleException("building.orderExists","An application already exists for this building.");
        if(!village.Structures.Any(s=>s.PlotId==plot)&&!village.Storages.Any(s=>s.PlotId==plot))
            throw new GameRuleException("building.notBuilt","Select a completed building.");
        var privateOwner = village.Structures.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId ?? village.Storages.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId;
        if (privateOwner is not null && privateOwner != actor)
            throw new GameRuleException("building.private", "Only the private owner can request this operation.");
        var structure = village.Structures.SingleOrDefault(s=>s.PlotId==plot);
        var storage = village.Storages.SingleOrDefault(s=>s.PlotId==plot);
        if(operation=="upgrade" && (structure?.RuinedAt is not null||storage?.RuinedAt is not null))throw new GameRuleException("building.ruined","Restore the ruins first.");
        if (operation == "upgrade" && !(structure is { Kind: BuildingKind.Castle or BuildingKind.Longhouse or BuildingKind.Hut, Level: < 3 }
            || storage is not null && StorageLimits.Upgrade(storage.Kind) is not null))
            throw UpgradeUnavailable(structure, storage);
        if (actor != village.OwnerAccountId) RequireRequestCapacity(village, privateOwner);
        var order=new CivicOrder{PlotId=plot,Operation=operation,RequestedBy=actor,CreatedAt=now};
        village.CivicOrders.Add(order);return order;
    }

    private static GameRuleException UpgradeUnavailable(Structure? structure, StorageBuilding? storage)
    {
        if (structure is {Kind: BuildingKind.Hospital, Level: < 3})
            return new("building.hospitalUpgrade", "The Tsar can upgrade the hospital from its hospital card.");
        var atMaximum = structure is { Kind: BuildingKind.Castle or BuildingKind.Longhouse or BuildingKind.Hut or BuildingKind.Hospital, Level: >= 3 }
            || storage is { Kind: StorageKind.Woodyard or StorageKind.StoneYard or StorageKind.Granary };
        return atMaximum
            ? new("building.maxLevel", "The building has reached its maximum upgrade level.")
            : new("building.upgradeUnsupported", "No upgrade is available for this building type.");
    }

    private static void RequireRequestCapacity(Village village, Guid? owner)
    {
        // Заявки занимают место в очереди, но не создают площадку и не расходуют материалы.
        Guid? OrderOwner(CivicOrder order) => order.Operation == "build" ? null :
            village.Structures.SingleOrDefault(s => s.PlotId == order.PlotId)?.OwnerAccountId
            ?? village.Storages.SingleOrDefault(s => s.PlotId == order.PlotId)?.OwnerAccountId;
        var pending = village.CivicOrders.Count(o => o.Status == "pending"
            && o.Operation is "build" or "upgrade" or "demolish" && OrderOwner(o) == owner);
        var maximum = owner is null ? BuildingCatalog.MaximumOpenSites : 1;
        if (village.Sites.Count(s => s.OwnerAccountId == owner) + pending >= maximum)
            throw new GameRuleException("building.requestQueueFull", "The construction queue is full. You cannot submit an application.");
    }

    public static void Approve(Village village,CivicOrder order,IReadOnlyList<Citizen> citizens,DateTimeOffset now)
    {
        if(order.Status!="pending") throw new GameRuleException("building.orderProcessed","This application was already processed.");
        if(order.PlotId==FortificationUpgrades.Plot&&order.Operation=="upgrade"&&order.BuildingKind==BuildingKind.FortificationUpgrade)
        {FortificationUpgrades.Begin(village,order,now);return;}
        if (order.Operation == "build")
        {
            var kindToBuild = order.BuildingKind ?? throw new GameRuleException("building.notAvailable", "Select a building.");
            var plot = WorldMap.PlotPosition(order.PlotId) ?? throw new GameRuleException("building.cannotBuildHere", "Select a valid plot.");
            var blueprint = ConstructionRules.Place(village, kindToBuild, (int)plot.X, (int)plot.Y, now);
            blueprint.PlacedByAccountId=order.RequestedBy;
            blueprint.OrderId = order.Id;
            order.Status = "working";
            order.ApprovedAt = now;
            return;
        }
        var operationOwner = village.Structures.SingleOrDefault(s=>s.PlotId==order.PlotId)?.OwnerAccountId ?? village.Storages.SingleOrDefault(s=>s.PlotId==order.PlotId)?.OwnerAccountId;
        if(village.Sites.Count(s=>s.OwnerAccountId==operationOwner)>=(operationOwner is null ? BuildingCatalog.MaximumOpenSites : 1)||village.Sites.Any(s=>s.PlotId==order.PlotId))
            throw new GameRuleException("building.busy","Finish the existing work first.");
        var structure=village.Structures.SingleOrDefault(s=>s.PlotId==order.PlotId);
        var storage=village.Storages.SingleOrDefault(s=>s.PlotId==order.PlotId);
        var kind=structure?.Kind??(storage is not null?BuildingCatalog.ForStorage(storage.Kind):null)
            ??throw new GameRuleException("building.protected","This is a protected civic building.");
        if(WorldMap.PlotPosition(order.PlotId) is null||kind is BuildingKind.RopeBridge or BuildingKind.Hospital)
            throw new GameRuleException("building.protected","This is a protected building or crossing.");
        if(citizens.Any(c=>!c.CycleSettled&&(c.CurrentSiteId==order.PlotId ||
            GameCatalog.FindTask(c.CurrentTaskId??"")?.RequiredBuilding==kind)))
            throw new GameRuleException("building.inUse","Finish the work inside this building first.");
        var site=new ConstructionSite{Kind=kind,PlotId=order.PlotId,PlacedAt=now,OrderId=order.Id,Scale=1,OwnerAccountId=structure?.OwnerAccountId??storage?.OwnerAccountId};
        if (kind == BuildingKind.Castle && order.Operation == "demolish")
            throw new GameRuleException("building.protected", "The royal castle cannot be demolished.");
        if(order.Operation=="demolish")
        {
            if(storage?.Goods.Any(g=>g.Amount>0)==true) throw new GameRuleException("building.emptyStorage","Empty this storage before demolition.");
            var residents=citizens.Where(c=>c.HomePlotId==order.PlotId).ToList();
            ShelterRules.RequireRelocation(village,citizens,order.PlotId);
            var moves=ShelterRules.Relocation(village,citizens,order.PlotId);
            foreach(var resident in residents)ShelterRules.Move(village,citizens,[resident],moves[resident.Id],now);
            if(structure is not null) structure.Decommissioning=true;
            if(storage is not null) storage.Decommissioning=true;
            site.Operation="demolish";site.TargetLevel=0;
            site.OperationWork=Math.Max(3,(int)Math.Ceiling(BuildingCatalog.Get(kind).Stages.Sum(s=>s.Jobs.Where(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Labor).Sum(j=>j.Amount))*0.3));
        }
        else if(structure?.RuinedAt is not null||storage?.RuinedAt is not null)throw new GameRuleException("building.ruined","Restore the ruins first.");
        else if(structure is {Kind:BuildingKind.Castle,Level:<3})
        {site.Operation="castle-upgrade";site.TargetLevel=structure.Level+1;site.Scale=Math.Pow(2,structure.Level);}
        else if(storage is not null&&StorageLimits.Upgrade(storage.Kind) is not null) site.Operation="storage-upgrade";
        else if(structure is {Kind:BuildingKind.Longhouse or BuildingKind.Hut,Level:<3})
        {site.Operation="housing-upgrade";site.TargetLevel=structure.Level+1;}
        else throw UpgradeUnavailable(structure, storage);
        village.Sites.Add(site);order.Status="working";order.ApprovedAt=now;
        ShelterRules.Assign(village,citizens,now);
    }

    public static bool Finish(Village village,ConstructionSite site,DateTimeOffset now,Citizen? worker=null)
    {
        if(site.Operation is null)return false;
        if(site.Operation=="site-demolish")
        {
            if(site.Salvage.Count>0) village.SiteRecoveries.Add(new(){PlotId=site.PlotId,OwnerAccountId=site.OwnerAccountId,
                CreatedAt=now,Goods=site.Salvage.Select(g=>new StoredGood{Kind=g.Kind,Amount=g.Amount}).ToList()});
            var oldOrder=village.CivicOrders.SingleOrDefault(o=>o.Id==site.OrderId);
            if(oldOrder is not null)oldOrder.Status="cancelled";
            village.Sites.Remove(site);return true;
        }
        var structure=village.Structures.SingleOrDefault(s=>s.PlotId==site.PlotId);
        var storage=village.Storages.SingleOrDefault(s=>s.PlotId==site.PlotId);
        if(site.Operation=="demolish")
        {
            if(storage?.Goods.Any(g=>g.Amount>0)==true)throw new GameRuleException("building.emptyStorage","Empty this storage first.");
            var investment=structure?.Investment??storage?.Investment??[];
            var returns=investment.Select(g=>new StoredGood{Kind=g.Kind,Amount=g.Amount/4}).Where(g=>g.Amount>0).ToList();
            if (site.OwnerAccountId is {} owner)
            {
                if (worker is null || worker.AccountId != owner) throw new GameRuleException("building.private", "Only the owner can finish this work.");
                foreach (var good in returns)
                    if (Desertod.Domain.Economy.PersonalInventory.Room(worker,good.Kind) < good.Amount)
                        throw new GameRuleException("inventory.full", "Make room for the salvaged material first.");
                if (structure is not null) village.Structures.Remove(structure);
                if (storage is not null) village.Storages.Remove(storage);
                foreach (var good in returns) Desertod.Domain.Economy.PersonalInventory.Add(worker,good.Kind,good.Amount,0,basisKnown:false);
                var ownOrder=village.CivicOrders.SingleOrDefault(o=>o.Id==site.OrderId);
                if(ownOrder is not null)ownOrder.Status="completed";
                village.Sites.Remove(site); return true;
            }
            // Simulate ALL refunds together: total-capacity bins are shared between resources.
            var bins=village.Storages.Where(s=>s.PlotId!=site.PlotId && s.OwnerAccountId==null).Select(s=>new StorageBuilding
                {Kind=s.Kind,PlotId=s.PlotId,Decommissioning=s.Decommissioning,
                 Goods=s.Goods.Select(g=>new StoredGood{Kind=g.Kind,Amount=g.Amount,Sharpen=g.Sharpen}).ToList()}).ToList();
            foreach(var good in returns)
            {
                var remaining=good.Amount;
                foreach(var bin in bins) remaining-=bin.Put(good.Kind,remaining);
                if(remaining>0)throw new GameRuleException("building.returnSpace","Make room in storage for the recovered materials.");
            }
            if(structure is not null)village.Structures.Remove(structure);
            if(storage is not null)village.Storages.Remove(storage);
            foreach(var good in returns)village.Store(good.Kind,good.Amount);
        }
        else
        {
            if(site.Operation=="storage-upgrade"&&storage is not null)storage.Kind=StorageLimits.Upgrade(storage.Kind)!.Value;
            if(site.Operation is "housing-upgrade" or "castle-upgrade" && structure is not null)structure.Level=site.TargetLevel;
            AddInvestment(site,structure?.Investment??storage!.Investment);
        }
        var order=village.CivicOrders.SingleOrDefault(o=>o.Id==site.OrderId);
        if(order is not null)order.Status="completed";
        village.Sites.Remove(site);return true;
    }

    public static void AddInvestment(ConstructionSite site,List<StoredGood> destination)
    {
        foreach(var progress in site.Progress)
        {
            var job=BuildingCatalog.FindJob(progress.JobId);
            if(job is not {Kind:JobKind.Haul,Resource:{} resource})continue;
            var existing=destination.SingleOrDefault(g=>g.Kind==resource);
            if(existing is null){existing=new StoredGood{Kind=resource};destination.Add(existing);}
            existing.Amount+=progress.Done;
        }
    }
}
