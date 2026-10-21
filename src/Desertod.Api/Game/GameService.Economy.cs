using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record EconomyRequest(Guid RequestId, string Action, ResourceKind Resource = ResourceKind.Wood,
    int Amount = 1, long Price = 0, Guid? Target = null, int X = 0, int Y = 0, BuildingKind Building = BuildingKind.Hut,
    string? TaskId = null, int Cycles = 1, long Pay = 0, DateTimeOffset? ExpiresAt = null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? TaxQuoteId=null);
public sealed record MarketLotDto(Guid Id, Guid SellerAccountId, ResourceKind Resource, int Amount, long Price, string Status, string Seller);
public sealed record PersonalGoodDto(ResourceKind Kind, int Amount, long CostBasis)
{
    public bool CostBasisKnown { get; init; }
}
public sealed record LedgerDto(Guid Id, DateTimeOffset At, string Operation, string Asset, string Location, long Delta, string? Actor);
/// <summary>Stable display identities mapped to existing ledger assets; this does not convert balances.</summary>
public sealed record EconomyCurrencyDto(string Id, string LedgerAsset);
public sealed record EconomyCurrenciesDto(EconomyCurrencyDto Coins, EconomyCurrencyDto TestCandles)
{
    public static readonly EconomyCurrenciesDto Current = new(new("economic-gold", "DZK"), new("test-candle", "TEST_CANDLE"));
}
public sealed record EconomyView(long Coins, long TestCandles, long EarnedToday, long ProfitToday, long TaxPreview,
    bool TaxEnabled, int ExchangeRate, int DailyExchangeLimit, int ExchangedToday, int KingdomCandleRoom,
    DateTimeOffset DayResetsAt, long Treasury, IReadOnlyList<PersonalGoodDto> Inventory, IReadOnlyList<MarketLotDto> Lots,
    IReadOnlyList<LedgerDto> Journal, Guid? NextJournalBefore, DateTimeOffset StartedAt,
    int WorkPay, int ConstructionPay, IReadOnlyList<EconomyResident> Residents, IReadOnlyList<PrivateStorageDto> PrivateStorages, IReadOnlyDictionary<ResourceKind, int> StockPrices, IReadOnlyList<PrivatePropertyDto> PrivateProperty, DateTimeOffset? EnergyDrinkReadyAt)
{
    public TaxPolicyDto? TaxPolicy { get; init; }
    public long LatestTaxPolicyVersion { get; init; }
    public long TaxableProfitToday { get; init; }
    public long TaxPaidToday { get; init; }
    public IReadOnlyList<MySaleTaxDto> MySaleTaxes { get; init; } = [];
    public IReadOnlyList<SiteRecoveryDto> SiteRecoveries { get; init; } = [];
    public EconomyCurrenciesDto Currencies { get; init; } = EconomyCurrenciesDto.Current;
    public bool WorkContractsEnabled { get; init; }
    public bool CanManageWorkOrders { get; init; }
    public IReadOnlyList<WorkOrderDto> WorkOrders { get; init; } = [];
    public long WorkPayAwaitingRoom { get; init; }
    public IReadOnlyList<SupplyOrderDto> SupplyOrders { get; init; } = [];
    public IReadOnlyList<SupplyDeliveryDto> MySupplyDeliveries { get; init; } = [];
    public IReadOnlyList<TradeReserveDto> TradeReserves { get; init; } = [];
    public IReadOnlyDictionary<ResourceKind, int> StockForSale { get; init; } = new Dictionary<ResourceKind, int>();
}
public sealed record MySaleTaxDto(Guid TradeId,Guid LotId,long? PolicyVersion,long Price,long? CostBasis,long? RealizedProfit,
    long TaxAmount,string ExemptReason,DateTimeOffset At);
public sealed record SupplyOrderDto(Guid Id, ResourceKind Resource, int RequestedAmount, int RemainingAmount,
    int DeliveredAmount, long UnitPrice, long ReservedCoins, DateTimeOffset ExpiresAt, SupplyOrderStatus Status);
public sealed record SupplyDeliveryDto(Guid Id, Guid SupplyOrderId, ResourceKind Resource, int Amount,
    long PaidCoins, long CostBasis, bool CostBasisKnown, DateTimeOffset At);
public sealed record TradeReserveDto(ResourceKind Resource, int MinimumAmount);
public sealed record WorkOrderDto(Guid Id, string TaskId, int PolicyVersion, long PayPerCycle, int AvailableCycles,
    int AcceptedCycles, int CompletedCycles, long ReservedCoins, DateTimeOffset ExpiresAt, WorkOrderStatus Status);
public sealed record SiteRecoveryDto(Guid Id,string PlotId,bool Personal,IReadOnlyList<ResourceAmount> Goods);
public sealed record PrivatePropertyDto(string PlotId, BuildingKind Kind, int Level, bool Site, bool HasWork);
public sealed record EconomyResident(Guid AccountId, string Name);
public sealed record PrivateStorageDto(string PlotId, StorageKind Kind, IReadOnlyList<ResourceAmount> Goods);

public sealed partial class GameService
{
    private int EconomyNumber(string key, int fallback, int min, int max) =>
        Math.Clamp(configuration.GetValue<int?>("Game:Economy:" + key) ?? fallback, min, max);

    private static DateOnly EconomyDay(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);
    private static Guid StableOperation(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    private static string EconomyFingerprint(EconomyRequest request)
    {
        if(request.TaxQuoteId is not null)
            return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {request.RequestId,request.Action,request.Resource,
                request.Amount,request.Price,request.Target,request.X,request.Y,request.Building,request.TaxQuoteId})));
        // Keep receipts written before work-order fields were added retryable byte-for-byte.
        object body=request.Action is "enable-work-contracts" or "fund-treasury" or "publish-work-order" or "cancel-work-order" or "claim-work-pay"
            or "publish-supply-order" or "cancel-supply-order" or "fulfill-supply-order" or "set-trade-reserve"
            ? request
            : new {request.RequestId,request.Action,request.Resource,request.Amount,request.Price,request.Target,request.X,request.Y,request.Building};
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body)));
    }

    private static async Task<GameWallet> WalletAsync(DesertodDbContext db, Guid village, Guid account, DateTimeOffset now, CancellationToken ct)
    {
        var wallet = await db.GameWallets.SingleOrDefaultAsync(w => w.VillageId == village && w.AccountId == account, ct);
        if (wallet is null) { wallet = new GameWallet { VillageId = village, AccountId = account }; db.GameWallets.Add(wallet); }
        var day = EconomyDay(now);
        if (wallet.IncomeDay != day) {
            wallet.IncomeDay = day; wallet.EarnedToday = 0; wallet.ProfitToday = 0;
            wallet.TaxableProfitToday=0;wallet.TaxPaidToday=0;wallet.TaxRemainder=0;wallet.TaxRevision++;
        }
        if (wallet.ExchangeDay != day) { wallet.ExchangeDay = day; wallet.ExchangedToday = 0; }
        return wallet;
    }

    private static void MoneyEntry(DesertodDbContext db, Guid village, Guid operation, Guid? account, string action,
        string asset, string location, long amount, DateTimeOffset now)
    {
        if (amount == 0) return;
        db.LedgerEntries.Add(new LedgerEntry { VillageId = village, OperationId = operation, AccountId = account,
            Operation = action, Asset = asset, Location = location, Delta = amount, At = now });
    }

    private async Task PayUsefulWorkAsync(DesertodDbContext db, Village village, CycleReport? report, DateTimeOffset now, CancellationToken ct)
    {
        if (report is null || report.TaskId == "dismantle") return;
        var citizen = await db.Citizens.SingleAsync(c => c.Id == report.CitizenId, ct);
        if (citizen.CyclePrivateOutput) return;
        var useful = report.ProjectContribution > 0 || report.Repaired > 0 || report.TechnologyId is not null && report.PrimaryExperience > 0
            || report.Produced.Sum(x => x.Amount) > report.Lost.Sum(x => x.Amount);
        if (citizen.CyclePaymentMode == WorkPaymentMode.Reserved)
        {
            await FinishWorkOrderAsync(db, village, citizen, useful, false, now, ct);
            return;
        }
        if (citizen.CyclePaymentMode == WorkPaymentMode.Voluntary) return;
        if (!useful && !(GameCatalog.FindTask(report.TaskId)?.Effect == TaskEffect.Guard && report.PrimaryExperience > 0)) return;
        var id = StableOperation($"work:{citizen.Id}:{citizen.CycleStartedAt:O}:{report.TaskId}");
        if (await db.EconomyReceipts.AnyAsync(x => x.VillageId == village.Id && x.RequestId == id, ct)) return;
        var pay = report.ProjectContribution > 0 || report.Repaired > 0
            ? EconomyNumber("ConstructionPay", 20, 1, 1000) : EconomyNumber("WorkPay", 10, 1, 1000);
        var wallet = await WalletAsync(db, village.Id, citizen.AccountId, now, ct);
        EconomyRules.Credit(wallet, pay); wallet.EarnedToday = checked(wallet.EarnedToday + pay);
        db.EconomyReceipts.Add(new EconomyReceipt { VillageId = village.Id, RequestId = id, AccountId = citizen.AccountId, Fingerprint = "work", At = now });
        MoneyEntry(db, village.Id, id, citizen.AccountId, "wage", "DZK", "wallet", pay, now);
        MoneyEntry(db, village.Id, id, citizen.AccountId, "work-completed", "work:" + report.TaskId,
            report.SiteId is {} plot ? "site:" + plot : "village", Math.Max(1, report.ProjectContribution), now);
    }

    private async Task RequireEconomyMemberAsync(DesertodDbContext db, Village village, Guid account, CancellationToken ct)
    {
        if (village.OwnerAccountId != account && !await db.Citizens.AnyAsync(c => c.VillageId == village.Id && c.AccountId == account, ct))
            throw new UnauthorizedAccessException();
    }

    public async Task<EconomyView> GetEconomyAsync(string slug, Guid account, Guid? before, string? asset, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct);
            await RequireEconomyMemberAsync(db, village, account, ct);
            return await EconomyViewAsync(db, village, account, before, asset, ct);
        }, ct);

    public async Task<EconomyView> ChangeEconomyAsync(string slug, Guid account, EconomyRequest request, CancellationToken ct)
    {
        CitizenSummaryDto[] updates = [];
        var result = await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct);
            await RequireEconomyMemberAsync(db, village, account, ct);
            if (request.RequestId == Guid.Empty || !Enum.IsDefined(request.Resource))
                throw new GameRuleException("economy.request", "Refresh the operation and try again.");
            if(request.TaxQuoteId is not null && request.Action!="buy-lot")
                throw new GameRuleException("tax.quote.action","A tax quote can only confirm a market purchase.");
            var fingerprint = EconomyFingerprint(request);
            var prior = await db.EconomyReceipts.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.RequestId == request.RequestId, ct);
            if (prior is not null)
            {
                if (prior.AccountId != account || prior.Fingerprint != fingerprint)
                    throw new GameRuleException("economy.reusedRequest", "This request was already used for another operation.");
                var current = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == account, ct);
                updates = current is null ? [] : [current.ToSummaryDto(time.GetUtcNow())];
                return await EconomyViewAsync(db, village, account, null, null, ct);
            }
            var now = time.GetUtcNow();
            village.Explore(await LoadTripsAsync(db,village.Id,ct),now);
            var wallet = await WalletAsync(db, village.Id, account, now, ct);
            var state = await db.KingdomEconomies.SingleAsync(x => x.VillageId == village.Id, ct);
            // Both funds share the available treasury. Release unused expired money before
            // checking any new purchase/order, even when no menu was opened in between.
            await ExpireWorkOrdersAsync(db, village, state, now, ct);
            await ExpireSupplyOrdersAsync(db, village, state, now, ct);
            var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == account, ct);
            var affected = new HashSet<Guid>();
            if (citizen is not null) affected.Add(citizen.Id);
            var maximumAmount = request.Action == "set-trade-reserve" ? 100_000
                : request.Action == "publish-supply-order" ? 10_000 : PersonalInventory.PerResource;
            if (request.Amount < (request.Action == "set-trade-reserve" ? 0 : 1) || request.Amount > maximumAmount || request.Price is < 0 or > 1_000_000)
                throw new GameRuleException("economy.amount", "Choose an amount from 1 to 100 and a valid whole price.");
            Citizen Own()
            {
                var resident = citizen ?? throw new GameRuleException("economy.citizen", "A resident is required for this action.");
                if (resident.RecoveryPausedAt is not null || resident.InjuredUntil > now || resident.Recovery?.Until > now || resident.ExhaustedUntil > now || resident.DungeonRunId is not null)
                    throw new GameRuleException("inventory.unavailable", "Wait until recovery or the expedition is finished.");
                return resident;
            }
            void WalletChange(Guid who, long amount, string action) => MoneyEntry(db, village.Id, request.RequestId, who, action, "DZK", "wallet", amount, now);
            db.AuditActor = account; db.AuditOperation = request.Action;
            switch (request.Action)
            {
                case "publish-supply-order":
                case "cancel-supply-order":
                case "set-trade-reserve":
                case "fulfill-supply-order":
                    await ChangeSupplyOrderAsync(db, village, state, wallet, account, request,
                        request.Action == "fulfill-supply-order" ? Own() : null, now, ct);
                    break;
                case "enable-work-contracts":
                case "fund-treasury":
                case "publish-work-order":
                case "cancel-work-order":
                case "claim-work-pay":
                    await ChangeWorkOrderAsync(db, village, state, wallet, account, request, now, ct);
                    break;
                case "donate-stock":
                {
                    var donor = Own();
                    var available = PersonalInventory.Amount(donor, request.Resource);
                    if (available < request.Amount) throw new GameRuleException("inventory.missing", "You do not own enough of this material.");
                    var stored = village.Store(request.Resource, request.Amount);
                    if (stored == 0) throw new GameRuleException("storage.targetFull", "There is no room for these materials in public storage.");
                    PersonalInventory.Remove(donor, request.Resource, stored);
                    MoneyEntry(db, village.Id, request.RequestId, account, "donate-stock", request.Resource.ToString(), "personal", -stored, now);
                    MoneyEntry(db, village.Id, request.RequestId, account, "donate-stock", request.Resource.ToString(), "village", stored, now);
                    break;
                }
                case "claim-site-recovery":
                {
                    var recovery=village.SiteRecoveries.SingleOrDefault(r=>r.Id==request.Target);
                    if(recovery is null) break; // An already collected receipt cannot mint another refund.
                    if(recovery.OwnerAccountId is {} owner)
                    {
                        if(owner!=account)throw new UnauthorizedAccessException();
                        var recipient=Own();
                        foreach(var good in recovery.Goods)
                            if(PersonalInventory.Room(recipient,good.Kind)<good.Amount)throw new GameRuleException("inventory.full","Make room for the recovered materials.");
                        foreach(var good in recovery.Goods)PersonalInventory.Add(recipient,good.Kind,good.Amount,0,basisKnown:false);
                    }
                    else
                    {
                        if(village.OwnerAccountId!=account)Own();
                        foreach(var good in recovery.Goods)
                            if(village.Store(good.Kind,good.Amount)!=good.Amount)throw new GameRuleException("building.returnSpace","Make room in communal storage first.");
                    }
                    village.SiteRecoveries.Remove(recovery);
                    break;
                }
                case "energy-drink-coins":
                case "energy-drink-candles":
                {
                    await SyncHospitalAsync(db, village.Id, ct);
                    var buyer = Own();
                    HospitalRules.RequireReady(buyer, now);
                    VigorDrinks.RestoreFully(buyer, village, now);
                    if (request.Action == "energy-drink-coins")
                    {
                        EconomyRules.Spend(wallet, 300);
                        WalletChange(account, -300, "energy-drink");
                    }
                    else
                    {
                        if (wallet.TestCandles < 3) throw new GameRuleException("economy.candles", "Not enough test candles.");
                        wallet.TestCandles -= 3;
                        MoneyEntry(db, village.Id, request.RequestId, account, "energy-drink", "TEST_CANDLE", "wallet", -3, now);
                    }
                    break;
                }
                case "buy-stock":
                {
                    var buyer = Own();
                    // Count-only personal inventory cannot preserve an edge or tool wear.
                    // Trade fresh tools only; a worn tool remains in communal storage with its uses intact.
                    var toolDurability = ToolCatalog.Get(request.Resource)?.MaxDurability;
                    var plainStock = TradeReserveRules.PlainPublicStock(village, request.Resource);
                    if (!StorageCatalog.IsGood(request.Resource) || plainStock < request.Amount)
                        throw new GameRuleException("inventory.stockMissing", "The common storage does not have this material.");
                    if (await db.SupplyOrders.AnyAsync(o=>o.VillageId==village.Id && o.Resource==request.Resource
                        && o.Status==SupplyOrderStatus.Open && o.ExpiresAt>now,ct))
                        throw new GameRuleException("supplyOrder.publicSaleBlocked", "The kingdom is buying this resource. Public sales resume when the supply order closes.");
                    if (TradeReserveRules.ForSale(village, state, request.Resource) < request.Amount)
                        throw new GameRuleException("market.reserve", "This stock is reserved for the kingdom. Choose a smaller quantity.");
                    if (request.Price != 0 && request.Price != EconomyRules.StorePrice(request.Resource))
                        throw new GameRuleException("economy.priceChanged", "The price has changed. Refresh the menu.");
                    var cost = checked((long)EconomyRules.StorePrice(request.Resource) * request.Amount);
                    if (PersonalInventory.Room(buyer, request.Resource) < request.Amount)
                        throw new GameRuleException("inventory.full", "Your personal inventory is full.");
                    EconomyRules.Spend(wallet, cost);
                    if (village.Take(request.Resource, request.Amount, 0, toolDurability) != request.Amount)
                        throw new GameRuleException("inventory.stockMissing", "The common stock changed. Refresh the menu.");
                    PersonalInventory.Add(buyer, request.Resource, request.Amount, cost);
                    state.TreasuryCoins = checked(state.TreasuryCoins + cost);
                    WalletChange(account, -cost, "buy-stock");
                    MoneyEntry(db, village.Id, request.RequestId, null, "buy-stock", "DZK", "treasury", cost, now);
                    break;
                }
                case "deposit-private":
                {
                    var storage = village.Storages.SingleOrDefault(s => s.PlotId == WorldMap.PlotAt(request.X, request.Y) && s.OwnerAccountId == account)
                        ?? throw new GameRuleException("building.private", "Choose your private warehouse.");
                    PrivateWarehouse.Deposit(Own(), storage, request.Resource, request.Amount);
                    break;
                }
                case "transfer":
                {
                    var sender = Own();
                    var recipient = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == request.Target, ct)
                        ?? throw new GameRuleException("inventory.recipient", "Choose a resident in this kingdom.");
                    if (recipient.AccountId == account) throw new GameRuleException("inventory.recipient", "Choose another resident.");
                    if (PersonalInventory.Room(recipient, request.Resource) < request.Amount) throw new GameRuleException("inventory.full", "The recipient's inventory is full.");
                    var cost = PersonalInventory.RemoveWithCost(sender, request.Resource, request.Amount);
                    PersonalInventory.Add(recipient, request.Resource, request.Amount, cost.Basis, cost.Known);
                    affected.Add(recipient.Id);
                    break;
                }
                case "list":
                {
                    if (request.Price < 1) throw new GameRuleException("economy.amount", "Choose a positive whole price for the lot.");
                    if (await db.MarketLots.CountAsync(x => x.VillageId == village.Id && x.SellerAccountId == account && x.Status == "open", ct) >= 10)
                        throw new GameRuleException("market.limit", "You already have ten open lots.");
                    var cost = PersonalInventory.RemoveWithCost(Own(), request.Resource, request.Amount);
                    db.MarketLots.Add(new MarketLot { Id = request.RequestId, VillageId = village.Id, SellerAccountId = account,
                        Resource = request.Resource, Amount = request.Amount, Price = request.Price, CostBasis = cost.Basis, CostBasisKnown = cost.Known,
                        TaxPolicyVersion=(await ActiveTaxPolicyAsync(db,village.Id,now,ct))?.Version,Status = "open", CreatedAt = now });
                    break;
                }
                case "buy-lot":
                case "cancel-lot":
                {
                    var lot = await db.MarketLots.SingleOrDefaultAsync(x => x.Id == request.Target && x.VillageId == village.Id, ct)
                        ?? throw new GameRuleException("market.missing", "This lot no longer exists.");
                    if (lot.Status != "open") throw new GameRuleException("market.closed", "This lot is already closed.");
                    var seller = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == lot.SellerAccountId, ct)
                        ?? throw new GameRuleException("inventory.recipient", "The seller is no longer a resident.");
                    affected.Add(seller.Id);
                    if (request.Action == "cancel-lot")
                    {
                        if (lot.SellerAccountId != account) throw new UnauthorizedAccessException();
                        PersonalInventory.Add(seller, lot.Resource, lot.Amount, lot.CostBasis, lot.CostBasisKnown); lot.Status = "cancelled";
                    }
                    else
                    {
                        if (lot.SellerAccountId == account) throw new GameRuleException("market.own", "You cannot buy your own lot.");
                        if (PersonalInventory.Room(Own(), lot.Resource) < lot.Amount) throw new GameRuleException("inventory.full", "Your personal inventory is full.");
                        var sellerWallet = await WalletAsync(db, village.Id, lot.SellerAccountId, now, ct);
                        var tax=await SettleMarketTaxAsync(db,lot,account,sellerWallet,state,request,now,ct);
                        EconomyRules.Spend(wallet, lot.Price); EconomyRules.Credit(sellerWallet, lot.Price-tax);
                        if (lot.CostBasisKnown) sellerWallet.ProfitToday = checked(sellerWallet.ProfitToday + lot.Price - lot.CostBasis);
                        PersonalInventory.Add(Own(), lot.Resource, lot.Amount, lot.Price);
                        lot.Status = "sold"; lot.BuyerAccountId = account;
                        WalletChange(account, -lot.Price, "trade"); WalletChange(lot.SellerAccountId, lot.Price-tax, "trade");
                        MoneyEntry(db,village.Id,request.RequestId,lot.SellerAccountId,"sale-tax","DZK","treasury",tax,now);
                    }
                    lot.ClosedAt = now;
                    break;
                }
                case "exchange":
                {
                    var rate = EconomyNumber("ExchangeRate", 100, 1, 100_000);
                    var limit = EconomyNumber("DailyExchangeLimit", 5, 1, 20);
                    var pool = EconomyNumber("KingdomDailyCandles", 50, 1, 1000);
                    if (state.FundDay != EconomyDay(now)) { state.FundDay = EconomyDay(now); state.CandlesIssuedToday = 0; }
                    if (wallet.ExchangedToday + request.Amount > limit || state.CandlesIssuedToday + request.Amount > pool)
                        throw new GameRuleException("exchange.limit", "The personal or kingdom daily exchange limit is reached.");
                    var cost = checked((long)rate * request.Amount);
                    EconomyRules.Spend(wallet, cost); wallet.TestCandles = checked(wallet.TestCandles + request.Amount);
                    wallet.ExchangedToday += request.Amount; state.CandlesIssuedToday += request.Amount;
                    WalletChange(account, -cost, "exchange-burn");
                    MoneyEntry(db, village.Id, request.RequestId, account, "exchange", "TEST_CANDLE", "wallet", request.Amount, now);
                    break;
                }
                case "private-upgrade":
                case "private-demolish":
                case "private-cancel":
                {
                    var owner = Own(); var plot = WorldMap.PlotAt(request.X,request.Y);
                    if(request.Action=="private-cancel")
                    {
                        if(village.Sites.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId!=account) throw new UnauthorizedAccessException();
                        ConstructionRules.Cancel(village,plot);
                    }
                    else
                    {
                        if(village.Structures.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId!=account && village.Storages.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId!=account)
                            throw new UnauthorizedAccessException();
                        var order=BuildingOperations.Request(village,account,plot,request.Action=="private-upgrade"?"upgrade":"demolish",now);
                        var people=await db.Citizens.Where(c=>c.VillageId==village.Id && c.Status==CitizenStatus.Active).ToListAsync(ct);
                        BuildingOperations.Approve(village,order,people,now);
                    }
                    break;
                }
                case "private-build":
                {
                    var builder = Own();
                    if (!Enum.IsDefined(request.Building) || request.Building is BuildingKind.Castle or BuildingKind.Hospital or BuildingKind.RopeBridge)
                        throw new GameRuleException("building.privateKind", "This civic building requires the Tsar's approval.");
                    var area = WorldMap.Footprint(request.X, request.Y);
                    if (KingdomRules.Area(village).Overlaps(area)) throw new GameRuleException("building.publicApproval", "Inside the kingdom, ask the Tsar to approve construction.");
                    var site = ConstructionRules.PlacePrivate(village, builder.AccountId, request.Building, request.X, request.Y, now);
                    db.AuditOperation = "private-build:" + site.PlotId;
                    break;
                }
                default: throw new GameRuleException("economy.action", "Unknown economic action.");
            }
            db.EconomyReceipts.Add(new EconomyReceipt { VillageId = village.Id, RequestId = request.RequestId, AccountId = account, Fingerprint = fingerprint, At = now });
            await db.SaveChangesAsync(ct); changes.Mark(village.Id);
            updates = db.ChangeTracker.Entries<Citizen>().Where(e => affected.Contains(e.Entity.Id))
                .Select(e => e.Entity.ToSummaryDto(now)).ToArray();
            return await EconomyViewAsync(db, village, account, null, null, ct);
        }, ct);
        // Publish only after the financial transaction committed, so connected clients reload owned goods.
        foreach (var updated in updates) await notifier.CitizenUpdated(slug, updated);
        return result;
    }

    private async Task<EconomyView> EconomyViewAsync(DesertodDbContext db, Village village, Guid account, Guid? before, string? asset, CancellationToken ct)
    {
        var now = time.GetUtcNow(); var wallet = await WalletAsync(db, village.Id, account, now, ct);
        var state = await db.KingdomEconomies.SingleAsync(x => x.VillageId == village.Id, ct);
        await ExpireWorkOrdersAsync(db, village, state, now, ct);
        await ExpireSupplyOrdersAsync(db, village, state, now, ct);
        var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == account, ct);
        var query = db.LedgerEntries.AsNoTracking().Where(x => x.VillageId == village.Id);
        if (!string.IsNullOrWhiteSpace(asset) && asset.Length <= 40) query = query.Where(x => x.Asset == asset);
        if (before is {} cursor)
        {
            var entry = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(x => x.VillageId == village.Id && x.Id == cursor, ct);
            if (entry is null) throw new GameRuleException("ledger.cursor", "Refresh the journal.");
            query = query.Where(x => x.At < entry.At || x.At == entry.At && x.Id.CompareTo(entry.Id) < 0);
        }
        // Personal possessions and wallet details are visible only to their owner, even in the public journal.
        query = village.OwnerAccountId == account
            ? query.Where(x => !x.Location.StartsWith("personal:") && !x.Location.StartsWith("private-storage:") && !x.Location.StartsWith("private-site:") && !x.Location.StartsWith("private-recovery:"))
            : query.Where(x => x.AccountId == null || x.AccountId == account || x.Asset != "DZK" && x.Asset != "TEST_CANDLE" && !x.Location.StartsWith("personal:") && !x.Location.StartsWith("private-storage:") && !x.Location.StartsWith("private-site:") && !x.Location.StartsWith("private-recovery:"));
        var rows = await query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id).Take(51).ToListAsync(ct);
        var names = await db.Accounts.Where(a => rows.Select(x => x.AccountId).Contains(a.Id)).Select(a => new { a.Id, a.DisplayName }).ToDictionaryAsync(a => a.Id, a => a.DisplayName, ct);
        var journal = rows.Take(50).Select(x => new LedgerDto(x.Id, x.At, x.Operation, x.Asset, x.Location, x.Delta,
            x.AccountId is {} id ? names.GetValueOrDefault(id, "Resident") : null)).ToArray();
        var lots = await db.MarketLots.AsNoTracking().Where(x => x.VillageId == village.Id && (x.Status == "open" || x.SellerAccountId == account))
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var sellers = lots.Select(x=>x.SellerAccountId).Distinct().ToArray();
        var sellerNames = await db.Accounts.Where(a=>sellers.Contains(a.Id)).Select(a=>new{a.Id,a.DisplayName}).ToDictionaryAsync(a=>a.Id,a=>a.DisplayName,ct);
        var supplyKinds=await db.SupplyOrders.Where(o=>o.VillageId==village.Id && o.Status==SupplyOrderStatus.Open && o.ExpiresAt>now)
            .Select(o=>o.Resource).Distinct().ToArrayAsync(ct);
        var taxPolicy=await ActiveTaxPolicyAsync(db,village.Id,now,ct);
        await db.SaveChangesAsync(ct);
        return new(wallet.Coins, wallet.TestCandles, wallet.EarnedToday, wallet.ProfitToday,
            wallet.TaxPaidToday,taxPolicy?.Enabled==true,
            EconomyNumber("ExchangeRate", 100, 1, 100_000), EconomyNumber("DailyExchangeLimit", 5, 1, 20), wallet.ExchangedToday,
            Math.Max(0, EconomyNumber("KingdomDailyCandles", 50, 1, 1000) - (state.FundDay == EconomyDay(now) ? state.CandlesIssuedToday : 0)),
            new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero), state.TreasuryCoins,
            citizen?.PersonalGoods.Select(x => new PersonalGoodDto(x.Kind, x.Amount, x.CostBasis) { CostBasisKnown = x.CostBasisKnown }).ToArray() ?? [],
            lots.Select(x=>new MarketLotDto(x.Id,x.SellerAccountId,x.Resource,x.Amount,x.Price,x.Status,sellerNames.GetValueOrDefault(x.SellerAccountId,"Resident"))).ToArray(), journal, rows.Count > 50 ? rows[49].Id : null, state.StartedAt,
            EconomyNumber("WorkPay", 10, 1, 1000), EconomyNumber("ConstructionPay", 20, 1, 1000),
            await db.Citizens.AsNoTracking().Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
                .OrderBy(c => c.Name).Select(c => new EconomyResident(c.AccountId, c.Name)).ToListAsync(ct),
            village.Storages.Where(s => s.OwnerAccountId == account).Select(s => new PrivateStorageDto(s.PlotId, s.Kind, s.Goods.Select(g => ToolCatalog.Amount(g.Kind, g.Amount, g.Sharpen, g.Durability)).ToArray())).ToArray(),
            Enum.GetValues<ResourceKind>().Where(StorageCatalog.IsGood).ToDictionary(k=>k,EconomyRules.StorePrice),
            village.Structures.Where(s=>s.OwnerAccountId==account).Select(s=>new PrivatePropertyDto(s.PlotId,s.Kind,s.Level,false,false))
                .Concat(village.Storages.Where(s=>s.OwnerAccountId==account).Select(s=>new PrivatePropertyDto(s.PlotId,BuildingCatalog.ForStorage(s.Kind)??BuildingKind.Heap,1,false,false)))
                .Concat(village.Sites.Where(s=>s.OwnerAccountId==account).Select(s=>new PrivatePropertyDto(s.PlotId,s.Kind,s.TargetLevel,true,s.HasWork||s.Operation!=null))).ToArray(), citizen is null ? null : VigorDrinks.EnergyDrinkReadyAt(citizen))
        { WorkContractsEnabled=state.WorkContractsEnabled, CanManageWorkOrders=village.OwnerAccountId==account,
            TaxPolicy=taxPolicy is null?null:TaxDto(taxPolicy),LatestTaxPolicyVersion=state.LatestTaxPolicyVersion,
            TaxableProfitToday=wallet.TaxableProfitToday,TaxPaidToday=wallet.TaxPaidToday,
            MySaleTaxes=await db.MarketTaxEvents.Where(e=>e.VillageId==village.Id&&e.SellerAccountId==account).OrderByDescending(e=>e.At).ThenByDescending(e=>e.Id).Take(30)
                .Select(e=>new MySaleTaxDto(e.Id,e.LotId,e.PolicyVersion,e.Price,e.CostBasisKnown?e.CostBasis:null,e.RealizedProfit,e.TaxAmount,e.ExemptReason,e.At)).ToArrayAsync(ct),
            TradeReserves=state.TradeReserves.Select(r=>new TradeReserveDto(r.Resource,r.MinimumAmount)).ToArray(),
            StockForSale=Enum.GetValues<ResourceKind>().Where(StorageCatalog.IsGood).ToDictionary(r=>r,r=>supplyKinds.Contains(r)?0:TradeReserveRules.ForSale(village,state,r)),
            SupplyOrders=await db.SupplyOrders.Where(o=>o.VillageId==village.Id && (o.Status==SupplyOrderStatus.Open || o.CreatedAt>now.AddDays(-1)))
                .OrderByDescending(o=>o.Status==SupplyOrderStatus.Open).ThenByDescending(o=>o.CreatedAt).Take(100)
                .Select(o=>new SupplyOrderDto(o.Id,o.Resource,o.RequestedAmount,o.RemainingAmount,o.DeliveredAmount,o.UnitPrice,o.ReservedCoins,o.ExpiresAt,o.Status)).ToArrayAsync(ct),
            MySupplyDeliveries=await db.SupplyDeliveries.Where(d=>d.VillageId==village.Id && d.SupplierAccountId==account)
                .OrderByDescending(d=>d.At).Take(20).Select(d=>new SupplyDeliveryDto(d.Id,d.SupplyOrderId,d.Resource,d.Amount,d.PaidCoins,d.CostBasis,d.CostBasisKnown,d.At)).ToArrayAsync(ct),
            WorkOrders=await db.WorkOrders.Where(o=>o.VillageId==village.Id && (o.Status==WorkOrderStatus.Open || o.ReservedCoins>0))
                .OrderBy(o=>o.ExpiresAt).Select(o=>new WorkOrderDto(o.Id,o.TaskId,o.PolicyVersion,o.PayPerCycle,o.AvailableCycles,o.AcceptedCycles,o.CompletedCycles,o.ReservedCoins,o.ExpiresAt,o.Status)).ToArrayAsync(ct),
            WorkPayAwaitingRoom=await db.WorkOrderClaims.Where(c=>c.VillageId==village.Id && c.AccountId==account && c.Status==WorkClaimStatus.Payable).SumAsync(c=>c.PromisedPay,ct),
            SiteRecoveries=village.SiteRecoveries.Where(r=>r.OwnerAccountId is null||r.OwnerAccountId==account)
            .Select(r=>new SiteRecoveryDto(r.Id,r.PlotId,r.OwnerAccountId is not null,r.Goods.Select(g=>new ResourceAmount(g.Kind,g.Amount)).ToArray())).ToArray() };
    }
}
