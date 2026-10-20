using Desertod.Domain.Catalog;

namespace Desertod.Domain.Economy;

public enum WorkOwnership { Legacy, PublicService, PublicContract, PrivateExtraction, PrivateCraft }
public enum WorkPaymentMode { Legacy, Voluntary, Reserved }
public enum WorkOrderStatus { Open, Cancelled, Expired, Completed }
public enum WorkClaimStatus { Working, Payable, Paid, Cancelled, Unproductive }

/// <summary>A bounded number of useful cycles, funded before publication. Accepted cycles retain their terms.</summary>
public sealed class WorkOrder
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid IssuerAccountId { get; set; }
    public required string TaskId { get; set; }
    public int PolicyVersion { get; set; } = 1;
    public long PayPerCycle { get; set; }
    public int AvailableCycles { get; set; }
    public int AcceptedCycles { get; set; }
    public int CompletedCycles { get; set; }
    public long ReservedCoins { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public WorkOrderStatus Status { get; set; }
}

public sealed class WorkOrderClaim
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid CitizenId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset CycleStartedAt { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long PromisedPay { get; set; }
    public WorkClaimStatus Status { get; set; }
}

public static class WorkOrderRules
{
    // Farm, construction and research have shared or staged results and extend this contract separately.
    public static bool Supports(TaskDefinition task) => task.Effect == TaskEffect.Resources && task.Outputs.Count > 0;

    public static long Budget(int cycles, long pay)
    {
        if (cycles is < 1 or > 1000 || pay is < 1 or > 1000)
            throw new GameRuleException("workOrder.terms", "Choose 1–1000 cycles and 1–1000 coins per useful cycle.");
        return checked(cycles * pay);
    }

    public static void Accept(WorkOrder order, DateTimeOffset now)
    {
        if (order.Status != WorkOrderStatus.Open || order.ExpiresAt <= now || order.AvailableCycles < 1)
            throw new GameRuleException("workOrder.closed", "This work order has no available cycles.");
        order.AvailableCycles--;
        order.AcceptedCycles++;
    }

    /// <returns>Unused money to return to the treasury; accepted promises remain reserved.</returns>
    public static long Close(WorkOrder order, WorkOrderStatus status)
    {
        if (order.Status != WorkOrderStatus.Open) return 0;
        if (status is not (WorkOrderStatus.Cancelled or WorkOrderStatus.Expired))
            throw new ArgumentOutOfRangeException(nameof(status));
        var refund = checked(order.AvailableCycles * order.PayPerCycle);
        order.ReservedCoins -= refund;
        order.AvailableCycles = 0;
        order.Status = status;
        return refund;
    }

    /// <returns>Refund when an unproductive/cancelled cycle cannot be offered again.</returns>
    public static long Finish(WorkOrder order, WorkOrderClaim claim, bool useful, bool cancelled, DateTimeOffset now)
    {
        if (claim.Status != WorkClaimStatus.Working) return 0;
        if (claim.WorkOrderId != order.Id || claim.VillageId != order.VillageId || claim.PromisedPay != order.PayPerCycle)
            throw new InvalidOperationException("Work claim does not match its funded order.");
        order.AcceptedCycles--;
        claim.FinishedAt = now;
        if (useful)
        {
            order.CompletedCycles++;
            claim.Status = WorkClaimStatus.Payable;
        }
        else
        {
            claim.Status = cancelled ? WorkClaimStatus.Cancelled : WorkClaimStatus.Unproductive;
            if (order.Status == WorkOrderStatus.Open && now < order.ExpiresAt) order.AvailableCycles++;
            else { order.ReservedCoins -= claim.PromisedPay; return claim.PromisedPay; }
        }
        if (order.Status == WorkOrderStatus.Open && order.AcceptedCycles == 0 && order.AvailableCycles == 0)
            order.Status = WorkOrderStatus.Completed;
        return 0;
    }

    public static bool Pay(WorkOrder order, WorkOrderClaim claim, GameWallet wallet)
    {
        if (claim.Status != WorkClaimStatus.Payable) return false;
        if (wallet.VillageId != claim.VillageId || wallet.AccountId != claim.AccountId || order.Id != claim.WorkOrderId)
            throw new InvalidOperationException("Work payment recipient does not match the claim.");
        if (wallet.Coins > EconomyRules.MaximumBalance - claim.PromisedPay) return false;
        EconomyRules.Credit(wallet, claim.PromisedPay);
        wallet.EarnedToday = checked(wallet.EarnedToday + claim.PromisedPay);
        order.ReservedCoins -= claim.PromisedPay;
        claim.Status = WorkClaimStatus.Paid;
        return true;
    }
}
