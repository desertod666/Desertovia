using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Director;
using Desertod.Domain.Economy;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DirectorFinanceApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private GameService Service => factory.Services.GetRequiredService<GameService>();
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static DirectorFinanceLimits Limits => new() { Enabled = true, WorkOrders = true, SupplyOrders = true,
        TreasuryIssues = true, TaxChanges = true, ActionsPerDay = 5, SpendingPerDay = 1000,
        MaximumUnitPay = 20, MaximumUnits = 10, MaximumIssue = 100,
        MaximumTaxBasisPoints = 1500, TaxStepBasisPoints = 100, TaxNoticeMinutes = 60, TaxCooldownMinutes = 120 };
    private async Task<Guid> Id(FoundedVillage world)
    { await using var db = await Db(); return await db.Villages.Where(x => x.Slug == world.Slug).Select(x => x.Id).SingleAsync(); }
    private static Task<DirectorFinanceView?> View(FoundedVillage world) => world.Tsar.GetFromJsonAsync<DirectorFinanceView>(world.Path + "/director/finance");
    private static Task<HttpResponseMessage> Grant(FoundedVillage world, long version, DirectorFinanceLimits? limits = null, Guid? request = null) =>
        world.Tsar.PutAsJsonAsync(world.Path + "/director/finance", new DirectorFinancePolicyRequest(request ?? Guid.NewGuid(), version, limits ?? Limits));
    private static async Task Mode(FoundedVillage world, string mode = "AutoEconomy") =>
        Assert.True((await world.Tsar.PutAsJsonAsync(world.Path + "/director/mode", new { mode })).IsSuccessStatusCode);
    private static async Task Economy(FoundedVillage world, EconomyRequest request) =>
        await Players.OkJsonAsync(await world.Tsar.PostAsJsonAsync(world.Path + "/economy", request));
    private async Task ArrangeNeed(FoundedVillage world, bool coins = true)
    {
        await players.ArrangeVillageAsync(world.Slug, (v, now) => v.Storages.Add(StorageBuilding.Create(StorageKind.TownHall, "13,5", now)));
        await Economy(world, new(Guid.NewGuid(), "enable-work-contracts"));
        await Economy(world, new(Guid.NewGuid(), "set-trade-reserve", ResourceKind.Wood, 10));
        if (coins)
        {
            await using var db = await Db(); var id = await Id(world);
            (await db.KingdomEconomies.SingleAsync(x => x.VillageId == id)).TreasuryCoins = 500;
            await db.SaveChangesAsync(); // Synthetic opening balance, never an asserted source of money.
        }
    }
    private async Task<AlmightyReview> Review(FoundedVillage world)
    {
        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        return (await Service.BeginAlmightyReviewAsync(await Id(world), default))!;
    }
    private Task<bool> Complete(AlmightyReview review, string option) =>
        Service.CompleteAlmightyReviewAsync(review, new(option, "Я вижу нехватку ресурсов и доступный фонд.", "Сравнить результат и новые нужды."), default);

    [Fact]
    public async Task Delegation_is_owner_only_versioned_replayable_and_does_not_enable_automatic_mode()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Finance authority");
        var member = await players.JoinAsync(world, "Finance member");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync(world.Path + "/director/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PutAsJsonAsync(world.Path + "/director/finance", new DirectorFinancePolicyRequest(Guid.NewGuid(), 0, Limits))).StatusCode);
        Assert.Equal(0, (await View(world))!.Version);
        await ArrangeNeed(world); var request = Guid.NewGuid();
        var first = await Players.OkJsonAsync(await Grant(world, 0, request: request));
        Assert.Equal(first.GetRawText(), (await Players.OkJsonAsync(await Grant(world, 0, request: request))).GetRawText());
        await Players.AssertRuleBrokenAsync(await Grant(world, 0, Limits with { MaximumIssue = 99 }, request), "director.finance.reused");
        await Players.AssertRuleBrokenAsync(await Grant(world, 0), "director.finance.stale");
        var review = await Review(world); Assert.DoesNotContain(review.Options, x => x.Id.StartsWith("finance_"));
        await Complete(review, "none"); Assert.Empty((await View(world))!.Actions);
        await Mode(world); review = await Review(world); Assert.Contains(review.Options, x => x.Id == "finance_work_low");
        Assert.DoesNotContain(review.Options, x => x.Id.StartsWith("finance_tax_")); // No complete known-profit evidence.
    }

    [Theory]
    [InlineData("finance_work_low")] [InlineData("finance_supply_mid")]
    public async Task Real_orders_are_backed_system_authored_once_and_next_review_compares_consequences(string option)
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Finance orders");
        await ArrangeNeed(world); await Players.OkJsonAsync(await Grant(world, 0)); await Mode(world);
        var review = await Review(world); Assert.Contains(review.Options, x => x.Id == option);
        Assert.True(await Complete(review, option)); Assert.False(await Complete(review, option));
        var view = (await View(world))!; var action = Assert.Single(view.Actions); Assert.Equal("director", action.Author);
        Assert.Equal(1, view.ActionsToday); Assert.True(view.ReservedToday > 0);
        await using var db = await Db(); var id = await Id(world);
        var economy = await db.KingdomEconomies.SingleAsync(x => x.VillageId == id);
        Assert.Equal(500 - view.ReservedToday, economy.TreasuryCoins);
        var postings = await db.LedgerEntries.Where(x => x.VillageId == id && x.Asset == "DZK").ToListAsync();
        Assert.Equal(2, postings.Count); Assert.All(postings, x => Assert.Equal(review.Lease, x.DirectorReviewId)); Assert.Equal(0, postings.Sum(x => x.Delta));
        Assert.Empty(await db.AlmightyProposals.Where(x => x.VillageId == id).ToListAsync());
        var next = await Review(world); using var snapshot = JsonDocument.Parse(next.Snapshot);
        Assert.Equal(option, snapshot.RootElement.GetProperty("finance").GetProperty("consequences").GetProperty("previous")[0].GetProperty("option").GetString());
        await Mode(world, "Off");
        Assert.Equal(view.ReservedToday, await db.WorkOrders.Where(x => x.VillageId == id).SumAsync(x => x.ReservedCoins)
            + await db.SupplyOrders.Where(x => x.VillageId == id).SumAsync(x => x.ReservedCoins));
    }

    [Theory]
    [InlineData("stock")] [InlineData("treasury")] [InlineData("midnight")] [InlineData("grant")] [InlineData("category")] [InlineData("payload")]
    public async Task Changed_evidence_or_authority_never_executes_a_stale_answer(string change)
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Finance fresh");
        await ArrangeNeed(world); await Players.OkJsonAsync(await Grant(world, 0)); await Mode(world);
        if (change == "midnight")
        {
            var now = factory.Clock.GetUtcNow(); factory.Clock.Advance(new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(1).AddSeconds(-30) - now);
        }
        var review = (await Service.BeginAlmightyReviewAsync(await Id(world), default))!;
        Assert.Contains(review.Options, x => x.Id == "finance_work_low");
        await using (var db = await Db())
        {
            var id = await Id(world);
            if (change == "stock") (await db.KingdomEconomies.SingleAsync(x => x.VillageId == id)).TradeReserves.Clear();
            if (change == "treasury") (await db.KingdomEconomies.SingleAsync(x => x.VillageId == id)).TreasuryCoins--;
            if (change == "payload")
            {
                var observation = await db.AlmightyObservations.SingleAsync(x => x.Id == review.Lease);
                observation.Snapshot = observation.Snapshot.Replace("\"unitPay\":5", "\"unitPay\":999");
                Assert.NotEqual(review.Snapshot, observation.Snapshot);
            }
            await db.SaveChangesAsync();
        }
        if (change == "midnight") factory.Clock.Advance(TimeSpan.FromSeconds(31));
        if (change == "grant") await Players.OkJsonAsync(await Grant(world, 1, Limits with { Enabled = false }));
        if (change == "category")
        {
            var state = (await world.Tsar.GetFromJsonAsync<AlmightyView>(world.Path + "/director"))!.State;
            await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/director/category/economy", new AlmightyCategoryRequest(Guid.NewGuid(), false, state.PolicyVersion)));
        }
        Assert.False(await Complete(review, "finance_work_low"));
        Assert.Empty((await View(world))!.Actions); Assert.Equal(0, (await View(world))!.ReservedToday);
    }

    [Fact]
    public async Task Treasury_issue_uses_existing_daily_fund_and_a_real_source_receipt_with_concurrent_completion_once()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Finance issue"); await ArrangeNeed(world, false);
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding",
            new TreasuryFundingPolicyRequest(Guid.NewGuid(), 0, true, factory.Clock.GetUtcNow(), 100)));
        await Players.OkJsonAsync(await Grant(world, 0)); await Mode(world); var review = await Review(world);
        Assert.Contains(review.Options, x => x.Id == "finance_issue");
        var results = await Task.WhenAll(Complete(review, "finance_issue"), Complete(review, "finance_issue"));
        Assert.Single(results, x => x); Assert.Equal(1, (await View(world))!.ActionsToday);
        await using var db = await Db(); var id = await Id(world);
        var receipt = Assert.Single(await db.TreasuryIssues.Where(x => x.VillageId == id).ToListAsync());
        Assert.Equal(Guid.Empty, receipt.ActorId); Assert.Equal(review.Lease, receipt.RequestId); Assert.InRange(receipt.Amount, 1, 100);
        var batch = Assert.Single(await db.MonetaryBatches.Where(x => x.VillageId == id).ToListAsync());
        Assert.True(batch.Finalized); Assert.Equal(receipt.Amount, batch.Issued);
        Assert.Contains((await Review(world)).Options, x => x.Id.StartsWith("finance_supply_"));
    }

    [Fact]
    public async Task Failed_final_commit_rolls_back_issue_action_budget_and_review_lease_together()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Finance rollback"); await ArrangeNeed(world, false);
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding", new TreasuryFundingPolicyRequest(Guid.NewGuid(), 0, true, factory.Clock.GetUtcNow(), 100)));
        await Players.OkJsonAsync(await Grant(world, 0)); await Mode(world); var review = await Review(world);
        await using var db = await Db(); var trigger = await EconomyCommitFailure.InstallFor(db, "TreasuryIssues", review.Lease);
        try { await Assert.ThrowsAnyAsync<Exception>(() => Complete(review, "finance_issue")); }
        finally { await EconomyCommitFailure.Remove(db, trigger, "TreasuryIssues"); }
        var view = (await View(world))!; Assert.Empty(view.Actions); Assert.Equal(0, view.ActionsToday); Assert.Equal(0, view.ReservedToday);
        var id = await Id(world); Assert.Empty(await db.TreasuryIssues.Where(x => x.VillageId == id).ToListAsync());
        Assert.Equal(review.Lease, (await db.AlmightyStates.SingleAsync(x => x.VillageId == id)).Lease);
        Assert.True(await Complete(review, "finance_issue"));
    }

    [Fact]
    public async Task Tax_changes_use_complete_known_profit_not_wages_or_unknown_basis_and_apply_only_after_notice()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Finance tax");
        var member = await players.JoinAsync(world, "Known profit actor"); var id = await Id(world);
        var firstFullDay = new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero).AddDays(1);
        // Explicit synthetic income history, not a claim about real people's earnings.
        await using (var db = await Db())
        {
            var owner = (await db.Villages.SingleAsync(x => x.Id == id)).OwnerAccountId;
            var seller = (await db.Citizens.SingleAsync(x => x.Id == member.Id)).AccountId;
            db.LedgerEntries.Add(new() { VillageId = id, OperationId = Guid.NewGuid(), At = factory.Clock.GetUtcNow(),
                Operation = "opening", Asset = "Wood", Location = "village", Delta = 1 });
            foreach (var (account, price, known) in new[] { (owner, 140L, true), (seller, 340L, true), (owner, 10000L, false) })
                db.MarketLots.Add(new() { VillageId = id, SellerAccountId = account, Resource = ResourceKind.Wood, Amount = 1,
                    Price = price, CostBasis = 40, CostBasisKnown = known, Status = "sold", CreatedAt = firstFullDay.AddHours(8), ClosedAt = firstFullDay.AddHours(9) });
            db.LedgerEntries.Add(new() { VillageId = id, AccountId = owner, OperationId = Guid.NewGuid(), At = firstFullDay.AddHours(10),
                Operation = "wage", Asset = "DZK", Location = "wallet", Delta = 1000 });
            await db.SaveChangesAsync();
        }
        factory.Clock.Advance(firstFullDay.AddDays(1).AddHours(8) - factory.Clock.GetUtcNow());
        await Players.OkJsonAsync(await Grant(world, 0)); await Mode(world); var review = await Review(world);
        Assert.Contains(review.Options, x => x.Id == "finance_tax_up");
        Assert.True(await Complete(review, "finance_tax_up"));
        await using var verify = await Db(); var policy = Assert.Single(await verify.TaxPolicies.Where(x => x.VillageId == id).ToListAsync());
        Assert.Equal(Guid.Empty, policy.ActorId); Assert.Equal(review.Lease, policy.RequestId);
        Assert.Equal(200, policy.Brackets[1].FromProfit); Assert.Equal(100, policy.Brackets[1].RateBasisPoints); // median of100/300
        Assert.Equal(0, policy.Brackets[0].RateBasisPoints); Assert.Equal(factory.Clock.GetUtcNow().AddHours(1), policy.EffectiveAt);
        Assert.Null((await world.Tsar.GetFromJsonAsync<TaxPolicyStateDto>(world.Path + "/economy/tax-policy"))!.Current);
        Assert.DoesNotContain((await Review(world)).Options, x => x.Id.StartsWith("finance_tax_"));
        factory.Clock.Advance(TimeSpan.FromHours(1));
        Assert.NotNull((await world.Tsar.GetFromJsonAsync<TaxPolicyStateDto>(world.Path + "/economy/tax-policy"))!.Current);
        Assert.DoesNotContain((await Review(world)).Options, x => x.Id.StartsWith("finance_tax_")); // cooldown still active
        factory.Clock.Advance(TimeSpan.FromHours(1));
        var lower = await Review(world); Assert.Contains(lower.Options, x => x.Id == "finance_tax_down");
        Assert.True(await Complete(lower, "finance_tax_down"));
        Assert.Equal(2, await verify.TaxPolicies.CountAsync(x => x.VillageId == id));
        Assert.True((await verify.TaxPolicies.AsNoTracking().SingleAsync(x => x.VillageId == id && x.Version == 1)).Enabled);
        Assert.False((await verify.TaxPolicies.AsNoTracking().SingleAsync(x => x.VillageId == id && x.Version == 2)).Enabled);
    }

    [Fact]
    public async Task Changing_delegation_does_not_reset_spent_allowance_but_a_new_utc_day_has_a_new_counter()
    {
        using var frozen = factory.Clock.Freeze();
        factory.Clock.Advance(new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero).AddDays(1).AddHours(8)-factory.Clock.GetUtcNow());
        var world = await players.FoundVillageAsync("Finance day"); await ArrangeNeed(world);
        await Players.OkJsonAsync(await Grant(world, 0, Limits with { ActionsPerDay = 1 })); await Mode(world);
        var review = await Review(world); Assert.True(await Complete(review, "finance_supply_mid"));
        var before = (await View(world))!; Assert.Equal(1, before.ActionsToday);
        await Players.OkJsonAsync(await Grant(world, 1, Limits with { ActionsPerDay = 1 }));
        Assert.Equal(before.ReservedToday, (await View(world))!.ReservedToday);
        Assert.DoesNotContain((await Review(world)).Options, x => x.Id.StartsWith("finance_"));
        var now = factory.Clock.GetUtcNow(); factory.Clock.Advance(new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(1).AddMinutes(1) - now);
        Assert.Equal(0, (await View(world))!.ActionsToday); Assert.Equal(0, (await View(world))!.ReservedToday);
        // The existing funded order still accounts for the shortage: a new budget does not order it again.
        Assert.DoesNotContain((await Review(world)).Options, x => x.Id.StartsWith("finance_work_") || x.Id.StartsWith("finance_supply_"));
        Assert.Single((await View(world))!.Actions);
    }

    [Fact]
    public async Task Director_cleanup_retains_financial_admission_and_removes_only_unreferenced_old_observations()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Finance audit retention");
        await ArrangeNeed(world); await Players.OkJsonAsync(await Grant(world, 0)); await Mode(world);
        var review = await Review(world); Assert.True(await Complete(review, "finance_supply_mid")); await Mode(world, "Off");
        var id = await Id(world); var unreferenced = Guid.NewGuid();
        await using var db = await Db(); db.AlmightyObservations.Add(new() { Id = unreferenced, VillageId = id, At = factory.Clock.GetUtcNow() });
        await db.SaveChangesAsync(); factory.Clock.Advance(TimeSpan.FromDays(16));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Game:AlmightyEnabled"] = "true", ["Hosting:PrimaryVillage"] = world.Slug }).Build();
        var director = new AlmightyService(Service, factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>(),
            new NoModelBridge(), config, factory.Clock, NullLogger<AlmightyService>.Instance);
        await director.TickAsync(default);
        Assert.True(await db.AlmightyObservations.AnyAsync(x => x.Id == review.Lease));
        Assert.False(await db.AlmightyObservations.AnyAsync(x => x.Id == unreferenced));
        Assert.Single(await db.DirectorFinanceActions.Where(x => x.VillageId == id).ToArrayAsync());
    }

    private sealed class NoModelBridge : IAlmightyBridge
    {
        public Task<AlmightyAnswer?> AnalyzeAsync(AlmightyReview review,CancellationToken ct) => throw new InvalidOperationException("Off must not call a model.");
        public Task<AlmightyDelivery> NotifyAsync(AlmightyNotice notice,CancellationToken ct) => Task.FromResult(new AlmightyDelivery("Unavailable"));
        public Task<SramoslavReply?> SpeakAsync(object request,CancellationToken ct) => Task.FromResult<SramoslavReply?>(null);
    }
}
