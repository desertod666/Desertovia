using Desertod.Domain;
using Desertod.Domain.Dungeons;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;
using System.Security.Cryptography;
using System.Text.Json;

namespace Desertod.Api.Game;

public sealed record ResumeJourneyRequest(Guid RequestId,Guid JourneyId,DateTimeOffset BlockedAt);

public sealed partial class GameService
{
    public async Task<VillageStateDto> ResumeJourneyAsync(string slug,Guid actor,ResumeJourneyRequest request,CancellationToken ct)=>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);var tsar=village.OwnerAccountId==actor;
            var citizen=tsar?null:await db.Citizens.SingleOrDefaultAsync(c=>c.VillageId==village.Id&&c.AccountId==actor&&c.Status==CitizenStatus.Active,ct);
            if(!tsar&&citizen is null)throw new UnauthorizedAccessException();
            var trip=tsar?village.TsarTrip:citizen!.Trip;
            if(request.RequestId==Guid.Empty||trip is null||request.JourneyId!=trip.Id)
                throw new GameRuleException("travel.changed","The journey changed. Reload before continuing.");
            var fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
            if(trip.LastResumeRequestId==request.RequestId)
            {
                if(trip.LastResumeFingerprint!=fingerprint)throw new GameRuleException("travel.reusedRequest","This request already continued a different journey.");
                return await BuildStateAsync(db,village,time.GetUtcNow(),ct);
            }
            if(trip.BlockedAt!=request.BlockedAt)throw new GameRuleException("travel.changed","The journey changed. Reload before continuing.");
            var now=time.GetUtcNow();var openWork=citizen is not null&&!citizen.CycleSettled&&citizen.CurrentTaskId is not null;
            if(!(openWork&&citizen!.RestState==RestState.RestRequested))SafeRest.RequireActive(tsar?village.TsarRestState:citizen!.RestState);
            if(citizen is not null){HospitalRules.RequireReady(citizen,now);if(Desertod.Domain.Combat.CombatRules.IsInjured(citizen,now))throw new GameRuleException("citizen.injured","Wait for the wound to heal before travelling.");}
            if(tsar)Desertod.Domain.Combat.RoyalInjury.Refresh(village,now);
            if(tsar&&(village.TsarInjuredUntil>now||village.TsarHospitalizedAt is not null))throw new GameRuleException("tsar.injured","Wait for the wound to heal before travelling.");
            await RequireMovementReadyAsync(db,village.Id,actor,citizen?.Id,ct);
            var resumed=citizen is null?RegionalTravel.Resume(village,trip,now):RegionalTravel.Resume(citizen,village,now);
            resumed.LastResumeRequestId=request.RequestId;resumed.LastResumeFingerprint=fingerprint;
            if(tsar)village.TsarTrip=resumed;
            db.AuditActor=actor;db.AuditOperation="resume-journey";
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            if(citizen is not null)await notifier.CitizenUpdated(slug,citizen.ToSummaryDto(now));
            return await BuildStateAsync(db,village,now,ct);
        },ct);

    private static async Task RequireMovementReadyAsync(DesertodDbContext db,Guid village,Guid actor,Guid? citizen,CancellationToken ct)
    {
        await RequireNoRaidActorAsync(db,village,actor,citizen,ct);
        await RequireNoDungeonActorAsync(db,village,actor,ct);
    }
}
