using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record RoyalHealRequest(Guid RequestId);

public sealed partial class GameService
{
    public async Task<VillageStateDto> HealRoyalAsync(string slug,Guid actor,RoyalHealRequest request,CancellationToken ct)=>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);
            if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
            if(request.RequestId==Guid.Empty)throw new GameRuleException("battle.requestId","A unique action identifier is required.");
            const string fingerprint="royal-injury-remedy-v1";
            var now=time.GetUtcNow();
            if(await db.EconomyReceipts.SingleOrDefaultAsync(r=>r.VillageId==village.Id&&r.RequestId==request.RequestId,ct) is {} prior)
            {
                if(prior.AccountId!=actor||prior.Fingerprint!=fingerprint)throw new GameRuleException("economy.reusedRequest","This request belongs to another operation.");
                return await BuildStateAsync(db,village,now,ct);
            }
            var citizen=await db.Citizens.SingleOrDefaultAsync(c=>c.VillageId==village.Id&&c.AccountId==actor,ct);
            await RequireMovementReadyAsync(db,village.Id,actor,citizen?.Id,ct);
            var helper=village.TsarRescueByCitizenId is {} helperId
                ?await db.Citizens.SingleOrDefaultAsync(c=>c.VillageId==village.Id&&c.Id==helperId,ct):null;
            var participants=new List<Desertod.Domain.Entities.Citizen>();
            if(citizen is not null)participants.Add(citizen);
            if(helper is not null&&helper.Id!=citizen?.Id)participants.Add(helper);
            RoyalInjury.Heal(village,participants,now);
            db.EconomyReceipts.Add(new(){VillageId=village.Id,AccountId=actor,RequestId=request.RequestId,Fingerprint=fingerprint,At=now});
            db.AuditActor=actor;db.AuditOperation="royal-injury-remedy";
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            if(citizen is not null)await notifier.CitizenUpdated(slug,citizen.ToSummaryDto(now));
            if(helper is not null&&helper.Id!=citizen?.Id)await notifier.CitizenUpdated(slug,helper.ToSummaryDto(now));
            return await BuildStateAsync(db,village,now,ct);
        },ct);
}
