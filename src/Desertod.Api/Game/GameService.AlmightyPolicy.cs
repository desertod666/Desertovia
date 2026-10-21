using Desertod.Domain;
using Desertod.Domain.Director;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    public async Task<AlmightyPolicyChange> SetAlmightyCategoryAsync(string slug,Guid actor,string kind,
        AlmightyCategoryRequest request,CancellationToken ct)
    {
        if(!AlmightyPermissions.ControllableKinds.Contains(kind,StringComparer.Ordinal))
            throw new GameRuleException("director.category","Неизвестная категория действий Всевышнего.");
        if(request.RequestId==Guid.Empty || request.Enabled is null || request.ExpectedPolicyVersion is null or <0)
            throw new GameRuleException("director.policy.request","Нужны идентификатор запроса, состояние категории и версия настроек.");
        return await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>{
            var village=await LoadVillageAsync(db,slug,ct);
            if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
            db.AuditActor=actor;db.AuditOperation="director-category";
            var previous=await db.AlmightyPolicyChanges.SingleOrDefaultAsync(p=>p.VillageId==village.Id&&p.RequestId==request.RequestId,ct);
            if(previous is not null)
            {
                if(previous.ActorId!=actor || previous.Kind!=kind || previous.Enabled!=request.Enabled
                    || previous.ExpectedPolicyVersion!=request.ExpectedPolicyVersion)
                    throw new GameRuleException("director.policy.reused","Этот идентификатор уже использован для другого запроса.");
                // Return the original acknowledgement, even if later requests changed the policy again.
                // A retry must neither reapply an old stop nor erase its durable receipt.
                return previous;
            }
            var now=time.GetUtcNow();
            var state=await DirectorStateAsync(db,village.Id,now,ct);
            if(state.PolicyVersion!=request.ExpectedPolicyVersion)
                throw new GameRuleException("director.policy.stale","Настройки изменились. Обновите их перед новым запросом.");
            var changed=state.DisabledKinds.Contains(kind)==request.Enabled.Value;
            var superseded=new List<Guid>();
            if(changed)
            {
                if(state.Lease is {} lease && await db.AlmightyObservations.SingleOrDefaultAsync(o=>o.Id==lease&&o.VillageId==village.Id,ct) is {} observation)
                    observation.Reason="Анализ отменён: царь изменил разрешённые категории действий.";
                state.DisabledKinds=request.Enabled.Value?state.DisabledKinds.Where(k=>k!=kind).ToArray()
                    :state.DisabledKinds.Append(kind).Order(StringComparer.Ordinal).ToArray();
                state.PolicyVersion++;state.Version++;state.Lease=null;state.LeaseUntil=null;state.NextReviewAt=now;
                if(!request.Enabled.Value)
                    foreach(var proposal in await db.AlmightyProposals.Where(p=>p.VillageId==village.Id&&p.Status=="Pending").ToListAsync(ct))
                        if(AlmightyPermissions.KindForOptionId(proposal.Option)==kind)
                        {
                            proposal.Status="Superseded";proposal.DecidedAt=now;
                            proposal.Result="Категория запрещена царём. Уже принятые события сохраняются.";
                            superseded.Add(proposal.Id);
                        }
            }
            var receipt=new AlmightyPolicyChange{VillageId=village.Id,RequestId=request.RequestId,ActorId=actor,Kind=kind,
                Enabled=request.Enabled.Value,ExpectedPolicyVersion=request.ExpectedPolicyVersion.Value,
                // PostgreSQL stores microseconds; first response and persisted retries must acknowledge exactly the same instant.
                PolicyVersion=state.PolicyVersion,Changed=changed,At=now.AddTicks(-(now.Ticks%10)),DisabledKinds=[..state.DisabledKinds],
                SupersededProposals=superseded.Order().ToArray()};
            db.AlmightyPolicyChanges.Add(receipt);
            await db.SaveChangesAsync(ct);
            return receipt;
        },ct);
    }
}
