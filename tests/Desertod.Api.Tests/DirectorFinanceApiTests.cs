using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Director;
using Desertod.Domain.Economy;
using Desertod.Domain.Storage;
using Desertod.Domain.Rules;
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

    [Fact]
    public async Task Shadow_choice_never_creates_money_orders_or_spending_and_an_old_shadow_answer_cannot_become_live()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Shadow finance fence");
        await ArrangeNeed(world); await Players.OkJsonAsync(await Grant(world,0,Limits with { ActionsPerDay=1 }));
        await Mode(world,"ShadowEconomy"); var shadow=await Review(world);
        Assert.True(await Complete(shadow,"finance_work_low")); Assert.False(await Complete(shadow,"finance_work_low"));
        var view=(await View(world))!; Assert.False(Assert.Single(view.Actions).Applied);
        Assert.Equal(0,view.ActionsToday); Assert.Equal(0,view.ReservedToday);
        await using var db=await Db(); var id=await Id(world);
        Assert.Equal(500,(await db.KingdomEconomies.SingleAsync(x=>x.VillageId==id)).TreasuryCoins);
        Assert.Empty(await db.WorkOrders.Where(x=>x.VillageId==id).ToArrayAsync());
        Assert.Empty(await db.SupplyOrders.Where(x=>x.VillageId==id).ToArrayAsync());
        Assert.Empty(await db.MonetaryBatches.Where(x=>x.VillageId==id).ToArrayAsync());
        Assert.Empty(await db.LedgerEntries.Where(x=>x.VillageId==id&&x.Asset=="DZK").ToArrayAsync());
        var late=await Review(world); await Mode(world); Assert.False(await Complete(late,"finance_work_low"));
        var live=await Review(world); Assert.True(await Complete(live,"finance_work_low"));
        view=(await View(world))!; Assert.Equal(2,view.Actions.Count); Assert.Single(view.Actions,x=>x.Applied);
        Assert.Equal(1,view.ActionsToday); Assert.True(view.ReservedToday>0);
        Assert.Single(await db.WorkOrders.Where(x=>x.VillageId==id).ToArrayAsync());
        await Mode(world,"Off"); factory.Clock.Advance(TimeSpan.FromDays(16));
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Game:AlmightyEnabled"]="true",["Hosting:PrimaryVillage"]=world.Slug }).Build();
        await new AlmightyService(Service,factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>(),
            new NoModelBridge(),config,factory.Clock,NullLogger<AlmightyService>.Instance).TickAsync(default);
        Assert.True(Assert.Single((await View(world))!.Actions).Applied); // hypothetical history expired, real audit remains
        Assert.True(await db.AlmightyObservations.AnyAsync(x=>x.Id==live.Lease));
        Assert.False(await db.AlmightyObservations.AnyAsync(x=>x.Id==shadow.Lease));
    }

    [Theory]
    [InlineData(1,false)] [InlineData(2,true)]
    public async Task A_small_issue_plans_a_reachable_order_and_never_spends_the_last_action_minting_unusable_partial_funds(int actions,bool canIssue)
    {
        using var frozen=factory.Clock.Freeze();var world=await players.FoundVillageAsync("Bounded useful issue");await ArrangeNeed(world,false);
        await Economy(world,new(Guid.NewGuid(),"set-trade-reserve",ResourceKind.Wood,100000));
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/funding",
            new TreasuryFundingPolicyRequest(Guid.NewGuid(),0,true,factory.Clock.GetUtcNow(),1000)));
        await Players.OkJsonAsync(await Grant(world,0,Limits with { ActionsPerDay=actions,MaximumUnits=1000,MaximumIssue=10,SupplyOrders=false,TaxChanges=false }));
        await Mode(world);var review=await Review(world);
        Assert.Equal(canIssue,review.Options.Any(x=>x.Id=="finance_issue"));
        if(!canIssue)
        {
            await Complete(review,"none");Assert.Empty((await View(world))!.Actions);return;
        }
        Assert.True(await Complete(review,"finance_issue"));var next=await Review(world);
        Assert.DoesNotContain(next.Options,x=>x.Id=="finance_issue");Assert.Contains(next.Options,x=>x.Id=="finance_work_low");
        Assert.True(await Complete(next,"finance_work_low"));
        await using var db=await Db();var id=await Id(world);var order=Assert.Single(await db.WorkOrders.Where(x=>x.VillageId==id).ToArrayAsync());
        Assert.Equal(2,order.AvailableCycles);Assert.Equal(5,order.PayPerCycle);Assert.Equal(10,order.ReservedCoins);
        Assert.Equal(10,Assert.Single(await db.TreasuryIssues.Where(x=>x.VillageId==id).ToArrayAsync()).Amount);
        Assert.Equal(0,(await db.KingdomEconomies.SingleAsync(x=>x.VillageId==id)).TreasuryCoins);
        Assert.Equal(2,(await View(world))!.ActionsToday);Assert.Equal(10,(await View(world))!.ReservedToday);
    }

    [Fact]
    public async Task Previously_saved_live_finance_envelope_without_execution_mode_stays_compatible()
    {
        using var frozen=factory.Clock.Freeze();var world=await players.FoundVillageAsync("Old finance envelope");
        await ArrangeNeed(world);await Players.OkJsonAsync(await Grant(world,0));await Mode(world);var review=await Review(world);
        await using(var db=await Db())
        {
            var observation=await db.AlmightyObservations.SingleAsync(x=>x.Id==review.Lease);
            var snapshot=System.Text.Json.Nodes.JsonNode.Parse(observation.Snapshot)!;
            Assert.True(snapshot["finance"]!.AsObject().Remove("executionMode"));
            observation.Snapshot=snapshot.ToJsonString();await db.SaveChangesAsync();
        }
        Assert.True(await Complete(review,"finance_work_low"));Assert.True(Assert.Single((await View(world))!.Actions).Applied);
    }

    [Fact]
    public async Task Shadow_tax_choice_neither_publishes_a_policy_nor_starts_the_real_tax_cooldown()
    {
        using var frozen=factory.Clock.Freeze(); var world=await players.FoundVillageAsync("Shadow tax");
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/tax-policy",
            new TaxPolicyRequest(Guid.NewGuid(),0,true,factory.Clock.GetUtcNow(),[new(){FromProfit=0,RateBasisPoints=500}])));
        await Players.OkJsonAsync(await Grant(world,0)); await Mode(world,"ShadowEconomy");
        var shadow=await Review(world); Assert.True(await Complete(shadow,"finance_tax_down"));
        await using var db=await Db(); var id=await Id(world);
        Assert.Equal(1,await db.TaxPolicies.CountAsync(x=>x.VillageId==id)); Assert.Equal(0,(await View(world))!.ActionsToday);
        await Mode(world); var live=await Review(world); Assert.Contains(live.Options,x=>x.Id=="finance_tax_down");
        Assert.True(await Complete(live,"finance_tax_down")); Assert.Equal(2,await db.TaxPolicies.CountAsync(x=>x.VillageId==id));
        Assert.DoesNotContain((await Review(world)).Options,x=>x.Id.StartsWith("finance_tax_"));
    }

    [Fact]
    public async Task Existing_director_tick_compares_shadow_then_funds_real_work_and_observes_once_paid_results_after_stop()
    {
        using var frozen=factory.Clock.Freeze();
        factory.Clock.Advance(new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date,TimeSpan.Zero).AddDays(1).AddHours(8)-factory.Clock.GetUtcNow());
        var world=await players.FoundVillageAsync("Director closed loop"); await ArrangeNeed(world,false);
        var first=await players.JoinAsync(world,"Loop producer A"); var second=await players.JoinAsync(world,"Loop producer B");
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path+"/economy/funding",
            new TreasuryFundingPolicyRequest(Guid.NewGuid(),0,true,factory.Clock.GetUtcNow(),1000)));
        await Players.OkJsonAsync(await Grant(world,0,Limits with { SupplyOrders=false,TaxChanges=false,MaximumUnitPay=100,MaximumUnits=2 }));
        await Mode(world,"ShadowEconomy");
        var bridge=new ScriptedLoopBridge(["finance_issue","finance_issue","finance_work_low","none"]);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Game:AlmightyEnabled"]="true",["Hosting:PrimaryVillage"]=world.Slug }).Build();
        var director=new AlmightyService(Service,factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>(),
            bridge,config,factory.Clock,NullLogger<AlmightyService>.Instance);
        await director.TickAsync(default); var id=await Id(world);
        await using var db=await Db(); Assert.Empty(await db.TreasuryIssues.Where(x=>x.VillageId==id).ToArrayAsync());
        Assert.False(Assert.Single((await View(world))!.Actions).Applied); Assert.Equal(0,(await View(world))!.ActionsToday);
        await Mode(world); await director.TickAsync(default);
        var source=Assert.Single(await db.TreasuryIssues.Where(x=>x.VillageId==id).ToArrayAsync()); Assert.Equal(50,source.Amount);
        factory.Clock.Advance(TimeSpan.FromMinutes(16)); await director.TickAsync(default);
        var order=Assert.Single(await db.WorkOrders.Where(x=>x.VillageId==id).ToArrayAsync());
        Assert.Equal("chop-wood",order.TaskId); Assert.Equal(25,order.PayPerCycle); Assert.Equal(2,order.AvailableCycles);
        foreach(var worker in new[]{first,second})
        {
            await Players.OkJsonAsync(await worker.Client.PostAsJsonAsync(world.Path+"/me/task",
                new{taskId="chop-wood",workOrderId=order.Id,requestId=Guid.NewGuid()}));
            await Players.OkJsonAsync(await worker.Client.PutAsJsonAsync(world.Path+"/me/rest",new{resting=true}));
        }
        await Mode(world,"Off"); factory.Clock.Advance(TimeSpan.FromHours(2));
        foreach(var worker in new[]{first,second})
        {
            await Players.OkJsonAsync(await worker.Client.GetAsync(world.Path+"/me"));
            await Players.OkJsonAsync(await worker.Client.GetAsync(world.Path+"/me"));
        }
        var wallets=await db.GameWallets.AsNoTracking().Where(x=>x.VillageId==id).ToArrayAsync();
        Assert.Equal(50,wallets.Sum(x=>x.Coins)); Assert.Equal(2,wallets.Count(x=>x.Coins==25));
        var treasury=await db.KingdomEconomies.AsNoTracking().SingleAsync(x=>x.VillageId==id); Assert.Equal(0,treasury.TreasuryCoins);
        var finalOrder=await db.WorkOrders.AsNoTracking().SingleAsync(x=>x.Id==order.Id); Assert.Equal(0,finalOrder.ReservedCoins);
        Assert.Equal(2,finalOrder.CompletedCycles); Assert.Equal(2,await db.WorkOrderClaims.CountAsync(x=>x.VillageId==id&&x.Status==WorkClaimStatus.Paid));
        Assert.Equal(50,await db.MonetaryBatches.Where(x=>x.VillageId==id).SumAsync(x=>x.Issued));
        Assert.False(await db.LedgerEntries.AnyAsync(x=>x.VillageId==id&&x.Operation=="wage"));
        Assert.All(await db.Citizens.Where(x=>x.VillageId==id).ToArrayAsync(),x=>Assert.Equal(RestState.Resting,x.RestState));
        await Mode(world,"ShadowEconomy"); await director.TickAsync(default);
        Assert.Equal(4,bridge.Reviews.Count); Assert.Equal(0,bridge.Notifications);
        using var observed=JsonDocument.Parse(bridge.Reviews[^1].Snapshot);
        var finance=observed.RootElement.GetProperty("finance");
        Assert.Equal("ShadowEconomy",finance.GetProperty("executionMode").GetString());
        Assert.False(finance.GetProperty("consequences").GetProperty("causalAttributionProven").GetBoolean());
        Assert.DoesNotContain(bridge.Reviews[^1].Options,x=>x.Id.StartsWith("finance_work_")); // No spendable treasury yet.
        var targets=finance.GetProperty("evidence").GetProperty("targets").EnumerateArray().ToArray();
        var wood=Assert.Single(targets,x=>x.GetProperty("resource").GetString()=="Wood");
        // New residents have zero stats: catalogue5 * existing stat factor0.4 =2 per useful cycle, not guaranteed5.
        Assert.Equal(4,wood.GetProperty("stock").GetInt32());Assert.Equal(10,wood.GetProperty("target").GetInt32());
        Assert.DoesNotContain(targets,x=>x.GetProperty("resource").GetString()=="Food"); // No food reserve was ordered and nobody is hungry yet.
        Assert.Equal(0,finance.GetProperty("evidence").GetProperty("sample").GetProperty("hungryResidents").GetInt32());
        var furtherIssue=Assert.Single(finance.GetProperty("commands").EnumerateArray(),x=>x.GetProperty("option").GetString()=="finance_issue");
        Assert.Equal((int)ResourceKind.Wood,furtherIssue.GetProperty("resource").GetInt32());Assert.Equal(24,furtherIssue.GetProperty("issue").GetInt64());
        Assert.Contains(bridge.Reviews[^1].Options,x=>x.Id=="finance_issue"); // Six wood remain; this is a future choice, not a second completed issue.
        var analysis=observed.RootElement.GetProperty("economy").GetProperty("analysis");
        Assert.Equal(50,analysis.GetProperty("currentPartialDay").GetProperty("wages").GetProperty("total").GetDecimal());
        Assert.Equal(50,analysis.GetProperty("funds").GetProperty("totalRecordedMoney").GetDecimal());
        var actions=(await View(world))!; Assert.Equal(3,actions.Actions.Count); Assert.Equal(2,actions.Actions.Count(x=>x.Applied));
        Assert.Equal(2,actions.ActionsToday); Assert.Equal(50,actions.ReservedToday);
        Assert.Equal(4,(await world.Tsar.GetFromJsonAsync<AlmightyView>(world.Path+"/director"))!.State.CallsToday);
    }

    private sealed class ScriptedLoopBridge(IEnumerable<string> options):IAlmightyBridge
    {
        private readonly Queue<string> remaining=new(options);
        public List<AlmightyReview> Reviews { get; }=[];
        public int Notifications { get; private set; }
        public Task<AlmightyAnswer?> AnalyzeAsync(AlmightyReview review,CancellationToken ct)
        {
            Reviews.Add(review); var option=remaining.Dequeue(); Assert.Contains(review.Options,x=>x.Id==option);
            return Task.FromResult<AlmightyAnswer?>(new(option,"Я вижу нужду и проверенный фонд. Сравню теневой выбор и результат.","Проверить еду, заработок и обязательства."));
        }
        public Task<AlmightyDelivery> NotifyAsync(AlmightyNotice notice,CancellationToken ct)
        { Notifications++; return Task.FromResult(new AlmightyDelivery("Unavailable")); }
        public Task<SramoslavReply?> SpeakAsync(object request,CancellationToken ct)=>Task.FromResult<SramoslavReply?>(null);
    }

    private sealed class NoModelBridge : IAlmightyBridge
    {
        public Task<AlmightyAnswer?> AnalyzeAsync(AlmightyReview review,CancellationToken ct) => throw new InvalidOperationException("Off must not call a model.");
        public Task<AlmightyDelivery> NotifyAsync(AlmightyNotice notice,CancellationToken ct) => Task.FromResult(new AlmightyDelivery("Unavailable"));
        public Task<SramoslavReply?> SpeakAsync(object request,CancellationToken ct) => Task.FromResult<SramoslavReply?>(null);
    }
}
