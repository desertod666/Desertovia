using Desertod.Domain.Buildings;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    private sealed record StockKey(string Asset, string Location, Guid? Owner);
    private sealed record StockSnapshot(bool Exists, Dictionary<StockKey, long> Goods);

    private static async Task<StockSnapshot> SnapshotStockAsync(DesertodDbContext db, Guid villageId, CancellationToken ct)
    {
        var village = await db.Villages.AsNoTracking().SingleOrDefaultAsync(v => v.Id == villageId, ct);
        if (village is null) return new(false, []);
        var goods = new Dictionary<StockKey, long>();
        void Add(string asset, string location, Guid? owner, long amount)
        {
            if (amount == 0) return;
            var key = new StockKey(asset, location, owner); goods[key] = goods.GetValueOrDefault(key) + amount;
        }
        foreach (var storage in village.Storages)
            foreach (var good in storage.Goods) Add(good.Kind.ToString(), (storage.OwnerAccountId is null ? "storage:" : "private-storage:") + storage.PlotId, storage.OwnerAccountId, good.Amount);
        foreach (var site in village.Sites)
            foreach (var progress in site.Progress)
                if (BuildingCatalog.FindJob(progress.JobId) is { Resource: {} resource, Kind: JobKind.Haul })
                    Add(resource.ToString(), (site.OwnerAccountId is null ? "site:" : "private-site:") + site.PlotId, site.OwnerAccountId, progress.Done);
        foreach(var site in village.Sites)
            foreach(var good in site.Salvage)Add(good.Kind.ToString(),(site.OwnerAccountId is null?"site:":"private-site:")+site.PlotId,site.OwnerAccountId,good.Amount);
        foreach(var recovery in village.SiteRecoveries)
            foreach(var good in recovery.Goods)Add(good.Kind.ToString(),(recovery.OwnerAccountId is null?"recovery:":"private-recovery:")+recovery.Id,recovery.OwnerAccountId,good.Amount);
        var citizens = await db.Citizens.AsNoTracking().Where(c => c.VillageId == villageId)
            .Select(c => new { c.AccountId, c.Load, c.PersonalGoods }).ToListAsync(ct);
        foreach (var citizen in citizens)
        {
            foreach (var good in citizen.Load) Add(good.Kind.ToString(), "hands:" + citizen.AccountId, citizen.AccountId, good.Amount);
            foreach (var good in citizen.PersonalGoods) Add(good.Kind.ToString(), "personal:" + citizen.AccountId, citizen.AccountId, good.Amount);
        }
        if(village.RoyalCombat is {} royal)
            foreach(var good in royal.Load)Add(good.Kind.ToString(),"hands:"+village.OwnerAccountId,village.OwnerAccountId,good.Amount);
        // Reserved lots still own physical goods: listing and buying are transfers, not production or loss.
        var lots = await db.MarketLots.AsNoTracking().Where(l => l.VillageId == villageId && l.Status == "open")
            .Select(l => new { l.Id, l.SellerAccountId, l.Resource, l.Amount }).ToListAsync(ct);
        foreach (var lot in lots)
            Add(lot.Resource.ToString(), "market:" + lot.Id, lot.SellerAccountId, lot.Amount);
        return new(true, goods);
    }

    private async Task<StockSnapshot> BeginAccountingAsync(DesertodDbContext db, Guid villageId, CancellationToken ct)
    {
        var before = await SnapshotStockAsync(db, villageId, ct);
        if (!before.Exists || await db.KingdomEconomies.AnyAsync(x => x.VillageId == villageId, ct)) return before;
        var now = time.GetUtcNow();
        db.KingdomEconomies.Add(new KingdomEconomy { VillageId = villageId, StartedAt = now, FundDay = EconomyDay(now) });
        var operation = Guid.CreateVersion7();
        foreach (var (key, amount) in before.Goods)
            MoneyEntry(db, villageId, operation, key.Owner, "opening", key.Asset, key.Location, amount, now);
        // Same transaction as the action: a failed request cannot leave a fake opening snapshot.
        await db.SaveChangesAsync(ct);
        return before;
    }

    private async Task FinishAccountingAsync(DesertodDbContext db, Guid villageId, StockSnapshot before, string operation, bool actionSaved, CancellationToken ct)
    {
        // Actions own and save their mutations. Re-saving the same nested JSON graph after a site
        // removal makes EF treat its shifted synthesized ordinals as edited read-only foreign keys.
        // A read-only action without a save may still have pending mutations; flush it once first.
        if (!actionSaved) await db.SaveChangesAsync(ct);
        // The action is finished; its result is a DTO/detached graph. Keep the transaction open,
        // but do not re-track already persisted Sites/Progress/Hands just to append journal rows.
        db.ChangeTracker.Clear();
        var after = await SnapshotStockAsync(db, villageId, ct);
        if (!after.Exists) return; // Legacy multi-world deletion cascades its journal, by design.
        var opId = Guid.CreateVersion7(); var now = time.GetUtcNow();
        foreach (var key in before.Goods.Keys.Union(after.Goods.Keys))
            MoneyEntry(db, villageId, opId, key.Owner ?? db.AuditActor, db.AuditOperation ?? operation,
                key.Asset, key.Location, after.Goods.GetValueOrDefault(key) - before.Goods.GetValueOrDefault(key), now);
        await db.SaveChangesAsync(ct);
    }
}
