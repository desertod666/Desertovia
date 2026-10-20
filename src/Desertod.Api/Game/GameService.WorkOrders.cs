using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    private static string Escrow(WorkOrder order) => "work-reserve:" + order.Id;

    private static void RefundWorkOrder(DesertodDbContext db, Village village, KingdomEconomy state,
        WorkOrder order, long refund, Guid operation, DateTimeOffset now)
    {
        if (refund == 0) return;
        state.TreasuryCoins = checked(state.TreasuryCoins + refund);
        MoneyEntry(db,village.Id,operation,null,"work-refund","DZK",Escrow(order),-refund,now);
        MoneyEntry(db,village.Id,operation,null,"work-refund","DZK","treasury",refund,now);
    }

    private static async Task ExpireWorkOrdersAsync(DesertodDbContext db, Village village, KingdomEconomy state, DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.WorkOrders.Where(o=>o.VillageId==village.Id && o.Status==WorkOrderStatus.Open && o.ExpiresAt<=now).ToListAsync(ct);
        foreach (var order in expired)
            RefundWorkOrder(db,village,state,order,WorkOrderRules.Close(order,WorkOrderStatus.Expired),StableOperation($"expire-work:{order.Id}"),now);
    }

    private static async Task AcceptWorkOrderAsync(DesertodDbContext db, Village village, Citizen citizen,
        Guid? orderId, DateTimeOffset now, CancellationToken ct)
    {
        var state=await db.KingdomEconomies.SingleAsync(e=>e.VillageId==village.Id,ct);
        citizen.CyclePaymentMode=state.WorkContractsEnabled ? WorkPaymentMode.Voluntary : WorkPaymentMode.Legacy;
        if (orderId is null) return;
        var order=await db.WorkOrders.SingleOrDefaultAsync(o=>o.Id==orderId && o.VillageId==village.Id,ct)
            ?? throw new GameRuleException("workOrder.closed","This work order is unavailable in this kingdom.");
        if (!state.WorkContractsEnabled || citizen.CyclePrivateOutput || order.TaskId!=citizen.CurrentTaskId
            || GameCatalog.FindTask(order.TaskId) is not {} task || !WorkOrderRules.Supports(task))
            throw new GameRuleException("workOrder.ownership","Choose matching public work for this order.");
        WorkOrderRules.Accept(order,now);
        var claim=new WorkOrderClaim { VillageId=village.Id,WorkOrderId=order.Id,CitizenId=citizen.Id,AccountId=citizen.AccountId,
            CycleStartedAt=citizen.CycleStartedAt!.Value,AcceptedAt=now,PromisedPay=order.PayPerCycle };
        db.WorkOrderClaims.Add(claim);
        citizen.CycleOwnership=WorkOwnership.PublicContract;
        citizen.CyclePaymentMode=WorkPaymentMode.Reserved;
        citizen.CycleWorkOrderClaimId=claim.Id;
    }

    private static async Task FinishWorkOrderAsync(DesertodDbContext db, Village village, Citizen citizen,
        bool useful, bool cancelled, DateTimeOffset now, CancellationToken ct)
    {
        if (citizen.CyclePaymentMode!=WorkPaymentMode.Reserved || citizen.CycleWorkOrderClaimId is not {} claimId) return;
        var claim=await db.WorkOrderClaims.SingleAsync(c=>c.Id==claimId && c.VillageId==village.Id,ct);
        var order=await db.WorkOrders.SingleAsync(o=>o.Id==claim.WorkOrderId && o.VillageId==village.Id,ct);
        var state=await db.KingdomEconomies.SingleAsync(e=>e.VillageId==village.Id,ct);
        RefundWorkOrder(db,village,state,order,WorkOrderRules.Finish(order,claim,useful,cancelled,now),claim.Id,now);
        if (claim.Status==WorkClaimStatus.Payable)
        {
            var wallet=await WalletAsync(db,village.Id,claim.AccountId,now,ct);
            PayWorkClaim(db,village,order,claim,wallet,now);
        }
    }

    private static void PayWorkClaim(DesertodDbContext db, Village village, WorkOrder order, WorkOrderClaim claim, GameWallet wallet, DateTimeOffset now)
    {
        if (!WorkOrderRules.Pay(order,claim,wallet)) return;
        MoneyEntry(db,village.Id,claim.Id,claim.AccountId,"contract-wage","DZK",Escrow(order),-claim.PromisedPay,now);
        MoneyEntry(db,village.Id,claim.Id,claim.AccountId,"contract-wage","DZK","wallet",claim.PromisedPay,now);
    }

    private static async Task ChangeWorkOrderAsync(DesertodDbContext db, Village village, KingdomEconomy state, GameWallet wallet,
        Guid account, EconomyRequest request, DateTimeOffset now, CancellationToken ct)
    {
        if (request.Action is "enable-work-contracts" or "publish-work-order" or "cancel-work-order" && village.OwnerAccountId!=account)
            throw new UnauthorizedAccessException();
        await ExpireWorkOrdersAsync(db,village,state,now,ct);
        switch(request.Action)
        {
            case "enable-work-contracts": state.WorkContractsEnabled=true; break;
            case "fund-treasury":
                EconomyRules.Spend(wallet,request.Price);
                state.TreasuryCoins=checked(state.TreasuryCoins+request.Price);
                MoneyEntry(db,village.Id,request.RequestId,account,"fund-treasury","DZK","wallet",-request.Price,now);
                MoneyEntry(db,village.Id,request.RequestId,account,"fund-treasury","DZK","treasury",request.Price,now);
                break;
            case "publish-work-order":
            {
                if (!state.WorkContractsEnabled || request.TaskId is null || GameCatalog.FindTask(request.TaskId) is not {} task || !WorkOrderRules.Supports(task))
                    throw new GameRuleException("workOrder.task","Choose a supported production task.");
                var budget=WorkOrderRules.Budget(request.Cycles,request.Pay);
                if (request.ExpiresAt is not {} expires || expires<=now || expires>now.AddDays(30))
                    throw new GameRuleException("workOrder.deadline","Choose a deadline within the next 30 days.");
                if (state.TreasuryCoins<budget) throw new GameRuleException("economy.funds","The treasury cannot reserve this work order.");
                if (await db.WorkOrders.CountAsync(o=>o.VillageId==village.Id && o.Status==WorkOrderStatus.Open,ct)>=50)
                    throw new GameRuleException("workOrder.limit","Close an existing order before publishing another.");
                var order=new WorkOrder{VillageId=village.Id,IssuerAccountId=account,TaskId=task.Id,
                    PayPerCycle=request.Pay,AvailableCycles=request.Cycles,ReservedCoins=budget,CreatedAt=now,ExpiresAt=expires};
                state.TreasuryCoins-=budget; db.WorkOrders.Add(order);
                MoneyEntry(db,village.Id,request.RequestId,account,"work-reserve","DZK","treasury",-budget,now);
                MoneyEntry(db,village.Id,request.RequestId,account,"work-reserve","DZK",Escrow(order),budget,now);
                break;
            }
            case "cancel-work-order":
            {
                var order=await db.WorkOrders.SingleOrDefaultAsync(o=>o.VillageId==village.Id && o.Id==request.Target,ct)
                    ?? throw new GameRuleException("workOrder.closed","This work order is unavailable in this kingdom.");
                RefundWorkOrder(db,village,state,order,WorkOrderRules.Close(order,WorkOrderStatus.Cancelled),request.RequestId,now);
                break;
            }
            case "claim-work-pay":
            {
                var claims=await db.WorkOrderClaims.Where(c=>c.VillageId==village.Id && c.AccountId==account && c.Status==WorkClaimStatus.Payable).ToListAsync(ct);
                foreach(var claim in claims)
                {
                    var order=await db.WorkOrders.SingleAsync(o=>o.Id==claim.WorkOrderId && o.VillageId==village.Id,ct);
                    PayWorkClaim(db,village,order,claim,wallet,now);
                }
                break;
            }
        }
    }
}
