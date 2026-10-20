using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    private static string SupplyFund(SupplyOrder order) => "supply-order:" + order.Id;

    private static void RefundSupplyOrder(DesertodDbContext db, Village village, KingdomEconomy state,
        SupplyOrder order, SupplyOrderStatus status, Guid operation, DateTimeOffset now)
    {
        var refund = SupplyOrderRules.Close(order, status);
        state.TreasuryCoins = checked(state.TreasuryCoins + refund);
        MoneyEntry(db,village.Id,operation,null,"supply-refund","DZK",SupplyFund(order),-refund,now);
        MoneyEntry(db,village.Id,operation,null,"supply-refund","DZK","treasury",refund,now);
    }

    private static async Task ExpireSupplyOrdersAsync(DesertodDbContext db, Village village, KingdomEconomy state, DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.SupplyOrders.Where(o=>o.VillageId==village.Id && o.Status==SupplyOrderStatus.Open && o.ExpiresAt<=now).ToListAsync(ct);
        foreach (var order in expired)
            RefundSupplyOrder(db,village,state,order,SupplyOrderStatus.Expired,StableOperation("supply-expire:"+order.Id),now);
    }

    private static async Task ChangeSupplyOrderAsync(DesertodDbContext db, Village village, KingdomEconomy state,
        GameWallet wallet, Guid account, EconomyRequest request, Citizen? supplier, DateTimeOffset now, CancellationToken ct)
    {
        if (request.Action != "fulfill-supply-order" && village.OwnerAccountId != account)
            throw new UnauthorizedAccessException();
        switch (request.Action)
        {
            case "set-trade-reserve":
                TradeReserveRules.Set(state,request.Resource,request.Amount);
                break;
            case "publish-supply-order":
            {
                if (!StorageCatalog.IsGood(request.Resource))
                    throw new GameRuleException("supplyOrder.resource","Choose a stored resource.");
                var budget=SupplyOrderRules.Budget(request.Amount,request.Price);
                if (request.ExpiresAt is not { } expires || expires<=now || expires>now.AddDays(30))
                    throw new GameRuleException("workOrder.deadline","Choose a deadline within the next 30 days.");
                if (state.TreasuryCoins<budget) throw new GameRuleException("economy.funds","The treasury cannot fund this supply order.");
                if (await db.SupplyOrders.CountAsync(o=>o.VillageId==village.Id && o.Status==SupplyOrderStatus.Open && o.ExpiresAt>now,ct)>=50)
                    throw new GameRuleException("workOrder.limit","Close an existing order before publishing another.");
                var order=new SupplyOrder{Id=request.RequestId,VillageId=village.Id,IssuerAccountId=account,
                    Resource=request.Resource,RequestedAmount=request.Amount,RemainingAmount=request.Amount,UnitPrice=request.Price,
                    ReservedCoins=budget,CreatedAt=now,ExpiresAt=expires};
                db.SupplyOrders.Add(order);state.TreasuryCoins-=budget;
                MoneyEntry(db,village.Id,request.RequestId,null,"supply-reserve","DZK","treasury",-budget,now);
                MoneyEntry(db,village.Id,request.RequestId,null,"supply-reserve","DZK",SupplyFund(order),budget,now);
                break;
            }
            case "cancel-supply-order":
            case "fulfill-supply-order":
            {
                var order=await db.SupplyOrders.SingleOrDefaultAsync(o=>o.VillageId==village.Id && o.Id==request.Target,ct)
                    ?? throw new GameRuleException("supplyOrder.closed","This supply order is not available in this kingdom.");
                if (request.Action=="cancel-supply-order")
                {
                    RefundSupplyOrder(db,village,state,order,SupplyOrderStatus.Cancelled,request.RequestId,now);
                    break;
                }
                if (supplier is null || supplier.Status!=CitizenStatus.Active)
                    throw new GameRuleException("economy.citizen","An active resident is required to deliver goods.");
                if (request.Resource!=order.Resource || request.Price!=order.UnitPrice)
                    throw new GameRuleException("economy.priceChanged","The order terms changed. Refresh the menu.");
                var pay=SupplyOrderRules.Deliver(order,request.Amount,now);
                if (village.FreeSpace(order.Resource)<request.Amount)
                    throw new GameRuleException("storage.targetFull","There is not enough public storage space for this delivery.");
                var cost=PersonalInventory.RemoveWithCost(supplier,order.Resource,request.Amount);
                EconomyRules.Credit(wallet,pay);
                if (village.Store(order.Resource,request.Amount)!=request.Amount)
                    throw new GameRuleException("storage.targetFull","The public storage changed. Try again.");
                if (cost.Known) wallet.ProfitToday=checked(wallet.ProfitToday+pay-cost.Basis);
                db.SupplyDeliveries.Add(new SupplyDelivery{Id=request.RequestId,VillageId=village.Id,SupplyOrderId=order.Id,
                    SupplierAccountId=account,Resource=order.Resource,Amount=request.Amount,PaidCoins=pay,
                    CostBasis=cost.Basis,CostBasisKnown=cost.Known,At=now});
                MoneyEntry(db,village.Id,request.RequestId,account,"supply-payment","DZK",SupplyFund(order),-pay,now);
                MoneyEntry(db,village.Id,request.RequestId,account,"supply-payment","DZK","wallet",pay,now);
                break;
            }
        }
    }
}
