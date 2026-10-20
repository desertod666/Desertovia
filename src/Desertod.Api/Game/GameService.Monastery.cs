using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record MonasteryRequest(Guid RequestId);
public sealed record MonasteryView(bool Open,bool InRange,bool KnowsRemedy,DateTimeOffset? ReadyAt,int Wood,int Food,int RemedyRoom,TilePoint Yard);
public sealed partial class GameService
{
    public static readonly TilePoint MonasteryYard=new(38.5,12.5);
    private async Task<MonasteryView> MonasteryViewAsync(Desertod.Infrastructure.Data.DesertodDbContext db,Village village,Guid actor,Citizen? citizen,DateTimeOffset now,CancellationToken ct)
    {
        var previous=await db.LedgerEntries.Where(e=>e.VillageId==village.Id&&e.AccountId==actor&&e.Operation=="monastery-aid").OrderByDescending(e=>e.At).Select(e=>(DateTimeOffset?)e.At).FirstOrDefaultAsync(ct);
        var position=actor==village.OwnerAccountId?village.TsarTrip?.PositionAt(now)??village.TsarSpot??WorldMap.Places[WorldMap.Camp]:citizen?.Trip?.PositionAt(now)??citizen?.Spot??citizen?.IdleSpot??WorldMap.Places[WorldMap.Camp];
        return new(village.BridgeOpened && village.BridgeRuinedAt is null,position.DistanceTo(MonasteryYard)<=2,TechnologyRules.Knows(village,TechnologyCatalog.HerbLore),previous?.AddHours(6),
            citizen is null?village.Stock(ResourceKind.Wood):PersonalInventory.Amount(citizen,ResourceKind.Wood),
            citizen is null?village.Stock(ResourceKind.Food):PersonalInventory.Amount(citizen,ResourceKind.Food),
            citizen is null?village.FreeSpace(ResourceKind.Remedy):PersonalInventory.Room(citizen,ResourceKind.Remedy),MonasteryYard);
    }
    public async Task<MonasteryView> GetMonasteryAsync(string slug,Guid actor,CancellationToken ct)=>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);await RequireEconomyMemberAsync(db,village,actor,ct);
            var citizen=actor==village.OwnerAccountId?null:await db.Citizens.SingleOrDefaultAsync(c=>c.VillageId==village.Id&&c.AccountId==actor,ct);
            return await MonasteryViewAsync(db,village,actor,citizen,time.GetUtcNow(),ct);
        },ct);

    public async Task<MonasteryView> TradeMonasteryAsync(string slug,Guid actor,MonasteryRequest request,CancellationToken ct)=>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);await RequireEconomyMemberAsync(db,village,actor,ct);
            var citizen=actor==village.OwnerAccountId?null:await db.Citizens.SingleAsync(c=>c.VillageId==village.Id&&c.AccountId==actor,ct);
            var now=time.GetUtcNow();
            if(request.RequestId==Guid.Empty)throw new GameRuleException("economy.request","A unique operation identifier is required.");
            var prior=await db.EconomyReceipts.SingleOrDefaultAsync(x=>x.VillageId==village.Id&&x.RequestId==request.RequestId,ct);
            if(prior is not null)
            {
                if(prior.AccountId!=actor||prior.Fingerprint!="monastery-aid-v1")throw new GameRuleException("economy.reusedRequest","This request belongs to another operation.");
                return await MonasteryViewAsync(db,village,actor,citizen,now,ct);
            }
            await SyncHospitalAsync(db,village.Id,ct,true,actor);
            if(HasHospitalRestriction(actor))throw new GameRuleException("hospital.unavailable","Wait until recovery.");
            if(citizen is not null){HospitalRules.RequireReady(citizen,now);if(!citizen.CycleSettled)throw new GameRuleException("defense.busy","Finish the current work first.");}
            if(village.OwnerAccountId==actor&&village.TsarInjuredUntil>now)throw new GameRuleException("defense.wounded","Wait until recovery.");
            if(citizen?.DungeonRunId is not null)throw new GameRuleException("dungeon.busy","Return from the dungeon first.");
            if(village.OwnerAccountId==actor)
            {
                var runs=await db.DungeonRuns.Where(d=>d.VillageId==village.Id&&d.Phase!=Desertod.Domain.Dungeons.DungeonPhase.Ended).ToListAsync(ct);
                if(runs.Any(d=>d.Heroes.Any(h=>h.AccountId==actor&&!h.Settled)))throw new GameRuleException("dungeon.busy","Return from the dungeon first.");
            }
            var battles=await db.RaidBattles.Where(b=>b.VillageId==village.Id&&b.FinishedAt==null).ToListAsync(ct);
            if(battles.Any(b=>b.Fighters.Any(f=>f.Active&&f.Health>0&&(f.IsTsar?f.AccountId==actor:f.CitizenId==citizen?.Id))))throw new GameRuleException("defense.busy","Leave combat first.");
            var view=await MonasteryViewAsync(db,village,actor,citizen,now,ct);
            if(!view.Open)throw new GameRuleException("bridge.closed","Build the Tsar Bridge first.");
            if(!view.InRange)throw new GameRuleException("monastery.distance","Come to the monastery courtyard first.");
            if(!view.KnowsRemedy)throw new GameRuleException("monastery.knowledge","Discover herb lore first.");
            if(view.ReadyAt>now)throw new GameRuleException("monastery.cooldown","The next contract is available in six hours.");
            if(view.Wood<10||view.Food<6)throw new GameRuleException("monastery.supplies","Bring ten wood and six food.");
            if(view.RemedyRoom<1)throw new GameRuleException("inventory.full","Make room for a remedy.");
            if(citizen is null){village.Take(ResourceKind.Wood,10);village.Take(ResourceKind.Food,6);village.Store(ResourceKind.Remedy,1);}
            else
            {
                var wood = PersonalInventory.RemoveWithCost(citizen, ResourceKind.Wood, 10);
                var food = PersonalInventory.RemoveWithCost(citizen, ResourceKind.Food, 6);
                PersonalInventory.Add(citizen, ResourceKind.Remedy, 1, checked(wood.Basis + food.Basis), wood.Known && food.Known);
            }
            MoneyEntry(db,village.Id,request.RequestId,actor,"monastery-aid","Wood","monastery",-10,now);
            MoneyEntry(db,village.Id,request.RequestId,actor,"monastery-aid","Food","monastery",-6,now);
            MoneyEntry(db,village.Id,request.RequestId,actor,"monastery-aid","Remedy","monastery",1,now);
            db.EconomyReceipts.Add(new(){VillageId=village.Id,RequestId=request.RequestId,AccountId=actor,Fingerprint="monastery-aid-v1",At=now});
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            if(citizen is not null)await notifier.CitizenUpdated(slug,citizen.ToSummaryDto(now));
            return await MonasteryViewAsync(db,village,actor,citizen,now,ct);
        },ct);
}
