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
        var battles=await db.RaidBattles.Where(b=>b.VillageId==village&&b.FinishedAt==null).ToListAsync(ct);
        if(battles.Any(b=>b.Fighters.Any(f=>f.Active&&f.Health>0&&(citizen is {} id?!f.IsTsar&&f.CitizenId==id:f.IsTsar&&f.AccountId==actor))))
            throw new GameRuleException("goto.retreatRequired","Retreat from combat before travelling.");
        var runs=await db.DungeonRuns.Where(r=>r.VillageId==village&&r.Phase!=DungeonPhase.Ended).ToListAsync(ct);
        if(runs.Any(r=>r.Heroes.Any(h=>h.AccountId==actor&&!h.Settled)))
            throw new GameRuleException("dungeon.busy","Return from the dungeon or leave its gathering first.");
    }
}
