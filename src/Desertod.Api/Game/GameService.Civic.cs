using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Desertod.Domain.Rules;
namespace Desertod.Api.Game;

public sealed record CivicRequest(string PlotId, string Operation, Guid? PartnerAccountId = null);
public sealed record CivicDecision(bool Approve);
public sealed partial class GameService
{
    public async Task<VillageStateDto> CivicRequestAsync(string slug, Guid actor, CivicRequest request, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            await SyncHospitalAsync(db, await FindVillageIdAsync(slug,ct), ct);
            var village = await LoadVillageAsync(db, slug, ct);
            var people = await db.Citizens.Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active).ToListAsync(ct);
            var citizen = people.SingleOrDefault(c => c.AccountId == actor);
            if (citizen is null && village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
            var now = time.GetUtcNow();
            if (request.Operation is "site-demolish" or "site-approve" or "site-cancel")
            {
                var site=ConstructionRules.FindSite(village,request.PlotId);
                if(!(site.OwnerAccountId==actor || village.OwnerAccountId==actor&&(site.OwnerAccountId is null||SiteManagement.LegacyConflict(site))))
                    throw new UnauthorizedAccessException();
                if(request.Operation=="site-cancel")ConstructionRules.Cancel(village,site.PlotId);
                else if(request.Operation=="site-approve")
                {
                    if(village.OwnerAccountId!=actor || !SiteManagement.LegacyConflict(site))throw new UnauthorizedAccessException();
                    site.BoundaryApproved=true;
                }
                else
                {
                    var workers=await db.Citizens.Where(c=>c.VillageId==village.Id&&c.CurrentSiteId==site.PlotId).ToListAsync(ct);
                    foreach(var worker in workers.Where(c=>!c.CycleSettled
                        &&c.RecoveryPausedAt is null&&c.CycleEndsAt<=now).ToArray())
                    {
                        var context=(await LoadContextAsync(db,village,worker.Id,now,ct)).Work;
                        var report=await SettleWorkAsync(db,worker,context,now,ct);
                        await AfterSettleAsync(db,village,report,now,ct);
                    }
                    if(!village.Sites.Contains(site))throw new GameRuleException("site.stageDone","This building is finished. Refresh its card.");
                    SiteManagement.BeginDismantle(village,site,actor,workers);
                }
                db.AuditActor=actor;db.AuditOperation=request.Operation+":"+request.PlotId;
            }
            else if (request.Operation == "housing")
            {
                if (citizen is null) throw new UnauthorizedAccessException();
                var home = village.Structures.SingleOrDefault(s => s.PlotId == request.PlotId && ShelterRules.Beds(s) > 0)
                    ?? throw new GameRuleException("housing.notFound", "Select a completed residential building.");
                if (home.OwnerAccountId is {} homeOwner && homeOwner != actor)
                    throw new GameRuleException("housing.private", "This home belongs to another resident.");
                if (home.OwnerAccountId == actor && request.PartnerAccountId is null)
                {
                    ShelterRules.Move(village, people, [citizen], request.PlotId, now);
                    foreach (var old in village.CivicOrders.Where(o => o.RequestedBy == actor && o.Operation == "housing" && o.Status == "pending"))
                        old.Status = "cancelled";
                    db.AuditActor = actor; db.AuditOperation = "housing-own:" + request.PlotId;
                }
                else
                {
                    if (village.CivicOrders.Any(o => o.RequestedBy == actor && o.Operation == "housing" && o.Status == "pending"))
                        throw new GameRuleException("housing.pending", "You already have a housing application.");
                    if (request.PartnerAccountId is {} partner && citizen.SpouseAccountId != partner)
                        throw new GameRuleException("housing.notSpouse", "Joint housing is available only with your confirmed spouse.");
                    village.CivicOrders.Add(new CivicOrder { PlotId=request.PlotId, Operation="housing", RequestedBy=actor,
                        PartnerAccountId=request.PartnerAccountId, CreatedAt=now });
                }
            }
            else
            {
                if (village.OwnerAccountId != actor && citizen?.IsGovernor != true) throw new UnauthorizedAccessException();
                if(request.Operation=="demolish")ShelterRules.RequireRelocation(village,people,request.PlotId);
                var order = BuildingOperations.Request(village, actor, request.PlotId, request.Operation, now);
                if (village.OwnerAccountId == actor) BuildingOperations.Approve(village, order, people, now);
            }
            db.AuditActor = actor;
            db.AuditOperation ??= "civic-request:" + request.Operation;
            await db.SaveChangesAsync(ct); changes.Mark(village.Id);
            foreach(var person in people)await notifier.CitizenUpdated(slug,person.ToSummaryDto(now));
            if(request.Operation=="site-cancel")return await BuildSavedStateAsync(db,slug,now,ct);
            return await BuildStateAsync(db, village, now, ct);
        }, ct);

    public Task<VillageStateDto> CivicDecisionAsync(string slug, Guid actor, Guid id, CivicDecision decision, CancellationToken ct) =>
        CivicDecisionCoreAsync(slug, actor, id, decision, null, ct);

    public Task<VillageStateDto> CivicTelegramDecisionAsync(string slug, string telegram, Guid id, bool approve, CancellationToken ct) =>
        CivicDecisionCoreAsync(slug, Guid.Empty, id, new(approve), telegram, ct);

    private async Task<VillageStateDto> CivicDecisionCoreAsync(string slug, Guid actor, Guid id, CivicDecision decision, string? telegram, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            await SyncHospitalAsync(db, await FindVillageIdAsync(slug,ct), ct);
            var village = await LoadVillageAsync(db, slug, ct);
            var order = village.CivicOrders.SingleOrDefault(o => o.Id == id) ?? throw new NotFoundException("building.orderMissing", "Application not found.");
            if (telegram is not null)
            {
                var ownerTelegram = await db.ExternalLogins.Where(l => l.AccountId == village.OwnerAccountId && l.Provider == "telegram")
                    .Select(l => l.ProviderUserId).SingleOrDefaultAsync(ct);
                if (telegram != ownerTelegram || order.Operation is not ("build" or "upgrade" or "demolish")) throw new UnauthorizedAccessException();
                actor = village.OwnerAccountId;
            }
            if (order.Status != "pending")
            {
                if (telegram is not null) return await BuildStateAsync(db, village, time.GetUtcNow(), ct);
                throw new GameRuleException("building.orderProcessed", "This application was already processed.");
            }
            var now=time.GetUtcNow();
            if (order.PartnerAccountId == actor && village.OwnerAccountId != actor)
            {
                if (decision.Approve) order.PartnerAccepted=true; else order.Status="rejected";
            }
            else
            {
                if (village.OwnerAccountId != actor) throw new UnauthorizedAccessException();
                if (!decision.Approve) order.Status="rejected";
                else
                {
                    var people=await db.Citizens.Where(c=>c.VillageId==village.Id && c.Status==CitizenStatus.Active).ToListAsync(ct);
                    if(order.Operation=="housing")
                    {
                        var resident=people.SingleOrDefault(c=>c.AccountId==order.RequestedBy) ?? throw new GameRuleException("housing.absent", "The resident is absent.");
                        var moving=new List<Citizen>{resident};
                        if(order.PartnerAccountId is {} partner)
                        {
                            if(!order.PartnerAccepted || resident.SpouseAccountId!=partner || resident.MarriageCheckedAt is null || now-resident.MarriageCheckedAt>TimeSpan.FromSeconds(90)) throw new GameRuleException("housing.consent", "Both spouses must agree first.");
                            moving.Add(people.Single(c=>c.AccountId==partner));
                        }
                        ShelterRules.Move(village,people,moving,order.PlotId,now); order.Status="completed";order.ApprovedAt=now;
                    }
                    else BuildingOperations.Approve(village,order,people,now);
                }
            }
            db.AuditActor = actor;
            db.AuditOperation = decision.Approve ? "civic-approve" : "civic-reject";
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            return await BuildStateAsync(db,village,now,ct);
        }, ct);
}
