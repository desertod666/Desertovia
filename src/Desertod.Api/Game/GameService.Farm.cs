using System.Security.Cryptography;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Entities;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
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
