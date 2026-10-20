using System.Security.Cryptography;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Entities;
using Desertod.Domain.Economy;
using Desertod.Domain.Farming;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    private static async Task RecordFarmChangesAsync(DesertodDbContext db,Village village,CycleReport? report,CancellationToken ct)
    {
        foreach(var snapshot in report?.FarmChanges??[])
        {
            var record=await db.FarmCrops.SingleOrDefaultAsync(c=>c.Id==snapshot.Id,ct);
            if(record is null)
            {
                record=new(){Id=snapshot.Id,VillageId=village.Id,PlotId=snapshot.PlotId,BedIndex=snapshot.Index,
                    Crop=snapshot.Crop,ContributionVersion=snapshot.ContributionVersion,SownAt=snapshot.SownAt};
                db.FarmCrops.Add(record);
            }
            if(record.VillageId!=village.Id || record.PlotId!=snapshot.PlotId || record.BedIndex!=snapshot.Index || record.Crop!=snapshot.Crop
                || record.ContributionVersion!=snapshot.ContributionVersion || record.SownAt!=snapshot.SownAt)
                throw new GameRuleException("farm.changed","The crop history does not match this accepted work.");
            if(record.HarvestedAt is not null)continue;
            foreach(var action in snapshot.Contributions)
                if(!record.Contributions.Any(c=>FarmContributionRules.SameWork(c,action)))record.Contributions.Add(FarmContributionRules.Copy(action));
            if(snapshot.HarvestedAt is {} harvested)
            {
                record.HarvestedAt=harvested;record.YieldFactor=snapshot.YieldFactor;
                record.Produced.AddRange(snapshot.Produced);record.Lost.AddRange(snapshot.Lost);
            }
        }
    }

    /// <summary>Public shared-farm history, like the village's visible fields; bounded and scoped to the selected world.</summary>
    public async Task<IReadOnlyList<FarmCropHistoryDto>> GetFarmHistoryAsync(string slug,string? plotId,Guid? before,CancellationToken ct)
    {
        var villageId=await FindVillageIdAsync(slug,ct);
        await using var db=await contextFactory.CreateDbContextAsync(ct);
        var village=await db.Villages.AsNoTracking().SingleAsync(v=>v.Id==villageId,ct);
        var query=db.FarmCrops.AsNoTracking().Where(c=>c.VillageId==villageId);
        if(plotId is not null)query=query.Where(c=>c.PlotId==plotId);
        if(before is {} cursor)query=query.Where(c=>c.Id.CompareTo(cursor)<0);
        var records=await query.OrderByDescending(c=>c.Id).Take(20).ToListAsync(ct);
        var living=village.Fields.Where(b=>b.Crop is not null).Select(b=>b.GenerationId).ToHashSet();
        return records.Select(c=>new FarmCropHistoryDto(c.Id,c.PlotId,c.BedIndex,c.Crop,c.SownAt,c.HarvestedAt,
            c.HarvestedAt is not null ? "Harvested" : living.Contains(c.Id) ? "Growing" : "Lost",
            c.Contributions.Select(FarmCareDto.From).ToArray(),
            c.Produced.Select(p=>new Desertod.Domain.Catalog.ResourceAmount(p.Resource,Math.Max(0,p.Amount-c.Lost.Where(l=>l.Resource==p.Resource).Sum(l=>l.Amount)))).Where(p=>p.Amount>0).ToArray(),c.Lost)).ToArray();
    }

    private static async Task<bool> FarmReceiptAsync(DesertodDbContext db, Village village, Guid actor,
        Guid requestId, object payload, DateTimeOffset now, CancellationToken ct)
    {
        if(requestId==Guid.Empty)throw new GameRuleException("economy.request","A request id is required.");
        var fingerprint="farm:"+Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload)));
        var prior=await db.EconomyReceipts.SingleOrDefaultAsync(r=>r.VillageId==village.Id && r.RequestId==requestId,ct);
        if(prior is not null)
        {
            if(prior.AccountId!=actor || prior.Fingerprint!=fingerprint)
                throw new GameRuleException("economy.requestConflict","This request id already has different conditions.");
            return true;
        }
        db.EconomyReceipts.Add(new(){VillageId=village.Id,AccountId=actor,RequestId=requestId,Fingerprint=fingerprint,At=now});
        return false;
    }

    public async Task<CitizenSummaryDto> SetFarmAccessAsync(string slug, Guid actor, Guid citizenId, FarmAccessRequest request, CancellationToken ct)
    {
        var changed=false;
        var result=await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);
            var owner=village.OwnerAccountId==actor;
            if(!owner && !await db.Citizens.AnyAsync(c=>c.VillageId==village.Id && c.AccountId==actor && c.Status==CitizenStatus.Active && c.IsGovernor && c.CanManageFarm,ct))
                throw new UnauthorizedAccessException();
            if(!owner && request.CanManageFarm is not null)throw new UnauthorizedAccessException();
            var citizen=await db.Citizens.SingleOrDefaultAsync(c=>c.Id==citizenId && c.VillageId==village.Id,ct)
                ?? throw new NotFoundException("citizen.notFound","This citizen does not live in the village.");
            if(request.IsFarmer is null && request.CanManageFarm is null)
                throw new GameRuleException("farm.access","Choose the farmer appointment to change.");
            if(request.CanManageFarm==true && !citizen.IsGovernor)
                throw new GameRuleException("farm.manager","First appoint this citizen Governor, then delegate farm management.");
            var now=time.GetUtcNow();
            if(!await FarmReceiptAsync(db,village,actor,request.RequestId,new{citizenId,request},now,ct))
            {
                if(request.IsFarmer is {} farmer)citizen.IsFarmer=farmer;
                if(request.CanManageFarm is {} manager)citizen.CanManageFarm=manager;
                db.AuditActor=actor;db.AuditOperation="farm-access";
                await db.SaveChangesAsync(ct);
                changed=true;
                changes.Mark(village.Id);
            }
            return citizen.ToSummaryDto(now);
        },ct);
        // Publish only after the authority transaction (including its receipt) committed.
        if(changed)
        {
            try {await notifier.CitizenUpdated(slug,result);}
            catch(Exception error) when(error is not OperationCanceledException)
            {logger.LogWarning("Committed farm appointment notification failed ({Type}).",error.GetType().Name);}
        }
        return result;
    }

    public async Task<VillageStateDto> EnableFarmerAdmissionAsync(string slug, Guid actor, FarmAdmissionRequest request, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);
            if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
            var now=time.GetUtcNow();
            if(!await FarmReceiptAsync(db,village,actor,request.RequestId,new{action="enable-farmer-admission"},now,ct))
            {
                village.FarmerAdmissionEnabled=true;
                db.AuditActor=actor;db.AuditOperation="farm-admission-enable";
                await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            }
            return await BuildStateAsync(db,village,now,ct);
        },ct);
}
