using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class TreasuryFundingApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static Task<System.Net.Http.HttpResponseMessage> Issue(FoundedVillage world, TreasuryIssueRequest request) =>
        world.Tsar.PostAsJsonAsync(world.Path + "/economy/treasury-issue", request);
    private static async Task<TreasuryFundingStateDto> State(FoundedVillage world) =>
        (await world.Tsar.GetFromJsonAsync<TreasuryFundingStateDto>(world.Path + "/economy/funding"))!;
    private static async Task<TreasuryIssueRequest> Request(FoundedVillage world, long amount)
    {
        var state = await State(world);
        return new(Guid.NewGuid(), state.Current?.Version ?? 1, state.BudgetDay, state.BudgetRevision, amount);
    }
    private async Task Policy(FoundedVillage world, long expected, long limit, bool enabled = true, DateTimeOffset? effective = null) =>
        await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding",
            new TreasuryFundingPolicyRequest(Guid.NewGuid(), expected, enabled, effective ?? factory.Clock.GetUtcNow(), limit)));
    private static async Task EnableWork(FoundedVillage world) => await Players.OkJsonAsync(await world.Tsar.PostAsJsonAsync(world.Path + "/economy",
        new EconomyRequest(Guid.NewGuid(), "enable-work-contracts")));

    [Fact]
    public async Task No_default_or_passive_issuance_and_explicit_policy_requires_funded_work_while_receipts_remain_replayable()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Declared funding");
        var first = await State(world); Assert.Equal(0, first.LatestVersion); Assert.Null(first.Current); Assert.Equal(0, first.RemainingAllowance);
        await Players.AssertRuleBrokenAsync(await Issue(world, await Request(world, 1)), "funding.disabled");
        await Policy(world, 0, 100, effective: factory.Clock.GetUtcNow().AddHours(1));
        Assert.Null((await State(world)).Current); await Players.AssertRuleBrokenAsync(await Issue(world, await Request(world, 1)), "funding.disabled");
        factory.Clock.Advance(TimeSpan.FromHours(1));
        await Players.AssertRuleBrokenAsync(await Issue(world, await Request(world, 30)), "funding.workMode");
        await EnableWork(world);
        var request = await Request(world, 30); var original = await Players.OkJsonAsync(await Issue(world, request));
        await Policy(world, 1, 0, enabled: false);
        Assert.Equal(original.GetRawText(), (await Players.OkJsonAsync(await Issue(world, request))).GetRawText());
        await Players.AssertRuleBrokenAsync(await Issue(world, request with { Amount = 31 }), "funding.request.reused");
        await Players.AssertRuleBrokenAsync(await Issue(world, request with { RequestId = Guid.NewGuid() }), "funding.disabled");
        await using var db = await Db(); var id = await db.Villages.Where(x => x.Slug == world.Slug).Select(x => x.Id).SingleAsync();
        Assert.Equal(30, (await db.KingdomEconomies.SingleAsync(x => x.VillageId == id)).TreasuryCoins);
        Assert.Equal(30, Assert.Single(await db.TreasuryIssueDays.Where(x => x.VillageId == id).ToListAsync()).Issued);
        Assert.Single(await db.TreasuryIssues.Where(x => x.VillageId == id).ToListAsync());
        var batch = Assert.Single(await db.MonetaryBatches.Where(x => x.VillageId == id).ToListAsync());
        Assert.True(batch.Finalized); Assert.Equal(30, batch.Issued); Assert.Equal("controlled-treasury-issuance", Assert.Single(batch.Operations).Kind);
    }

    [Fact]
    public async Task Version_changes_do_not_refill_a_daily_budget_concurrent_commands_and_midnight_require_fresh_state()
    {
        using var frozen = factory.Clock.Freeze();
        var morning = new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        factory.Clock.Advance(morning - factory.Clock.GetUtcNow());
        var world = await players.FoundVillageAsync("Daily funding bounds"); await EnableWork(world);
        await Policy(world, 0, 100); await Players.OkJsonAsync(await Issue(world, await Request(world, 80)));
        await Policy(world, 1, 50); Assert.Equal(80, (await State(world)).IssuedToday); Assert.Equal(0, (await State(world)).RemainingAllowance);
        await Players.AssertRuleBrokenAsync(await Issue(world, await Request(world, 1)), "funding.allowance");
        await Policy(world, 2, 100, effective: factory.Clock.GetUtcNow().AddHours(1));
        Assert.Equal(2, (await State(world)).Current!.Version); Assert.Equal(3, (await State(world)).LatestVersion);
        var early = (await Request(world, 1)) with { PolicyVersion = 3 };
        await Players.AssertRuleBrokenAsync(await Issue(world, early), "funding.policy.stale");
        factory.Clock.Advance(TimeSpan.FromHours(1));
        var request = await Request(world, 20);
        var replies = await Task.WhenAll(Issue(world, request), Issue(world, request with { RequestId = Guid.NewGuid() }));
        Assert.Single(replies, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(replies, x => x.StatusCode == HttpStatusCode.BadRequest);
        var finished = await State(world); Assert.Equal(100, finished.IssuedToday); Assert.Equal(0, finished.RemainingAllowance);
        var midnight = new DateTimeOffset(factory.Clock.GetUtcNow().UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        factory.Clock.Advance(midnight - factory.Clock.GetUtcNow());
        await Players.AssertRuleBrokenAsync(await Issue(world, request with { RequestId = Guid.NewGuid(), ExpectedBudgetRevision = finished.BudgetRevision, Amount = 1 }), "funding.day.stale");
        var next = await State(world); Assert.Equal(0, next.IssuedToday); Assert.Equal(100, next.RemainingAllowance); Assert.Equal(0, next.BudgetRevision);
        await Players.OkJsonAsync(await Issue(world, await Request(world, 100)));
        await using var db = await Db(); var id = await db.Villages.Where(x => x.Slug == world.Slug).Select(x => x.Id).SingleAsync();
        Assert.Equal(200, (await db.KingdomEconomies.SingleAsync(x => x.VillageId == id)).TreasuryCoins);
        Assert.Equal(2, await db.TreasuryIssueDays.CountAsync(x => x.VillageId == id));
        Assert.Equal(200, await db.MonetaryBatches.Where(x => x.VillageId == id).SumAsync(x => x.Issued));
    }

    [Fact]
    public async Task Policy_owner_scope_fingerprints_and_future_versions_are_checked_without_creating_money()
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Funding actor scope");
        var resident = await players.JoinAsync(world, "Observer"); var policy = new TreasuryFundingPolicyRequest(Guid.NewGuid(), 0, true, factory.Clock.GetUtcNow().AddHours(1), 100);
        await Players.AssertForbiddenAsync(await resident.Client.PutAsJsonAsync(world.Path + "/economy/funding", policy));
        var original = await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding", policy));
        Assert.Equal(original.GetRawText(), (await Players.OkJsonAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding", policy))).GetRawText());
        await Players.AssertRuleBrokenAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding", policy with { DailyIssueLimit = 101 }), "funding.request.reused");
        await Players.AssertRuleBrokenAsync(await world.Tsar.PutAsJsonAsync(world.Path + "/economy/funding", policy with { RequestId = Guid.NewGuid() }), "funding.policy.stale");
        var issue = new TreasuryIssueRequest(Guid.NewGuid(), 1, (await State(world)).BudgetDay, 0, 10);
        await Players.AssertForbiddenAsync(await resident.Client.PostAsJsonAsync(world.Path + "/economy/treasury-issue", issue));
        await Players.OkJsonAsync(await resident.Client.GetAsync(world.Path + "/economy/funding"));
        await using var db = await Db(); var id = await db.Villages.Where(x => x.Slug == world.Slug).Select(x => x.Id).SingleAsync();
        Assert.False(await db.TreasuryIssueDays.AnyAsync(x => x.VillageId == id)); Assert.False(await db.TreasuryIssues.AnyAsync(x => x.VillageId == id));
        Assert.Equal(0, (await db.KingdomEconomies.SingleAsync(x => x.VillageId == id)).TreasuryCoins);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_source_receipt_or_final_commit_failure_rolls_back_money_allowance_and_audit(bool failCommit)
    {
        using var frozen = factory.Clock.Freeze(); var world = await players.FoundVillageAsync("Atomic declared issue");
        await EnableWork(world); await Policy(world, 0, 100); var request = await Request(world, 60);
        await using var db = await Db(); var id = await db.Villages.Where(x => x.Slug == world.Slug).Select(x => x.Id).SingleAsync();
        string trigger;
        if (failCommit) trigger = await EconomyCommitFailure.InstallFor(db, "TreasuryIssues", request.RequestId);
        else
        {
            trigger = "remove_issue_" + Guid.NewGuid().ToString("N");
            var sql = $"CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"RequestId\"='{request.RequestId}'::uuid THEN DELETE FROM \"TreasuryIssues\" WHERE \"VillageId\"=NEW.\"VillageId\" AND \"RequestId\"=NEW.\"RequestId\"; END IF; RETURN NEW; END; $$; CREATE TRIGGER {trigger} AFTER INSERT ON \"TreasuryIssues\" FOR EACH ROW EXECUTE FUNCTION {trigger}();";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        try
        {
            var response = await Issue(world, request);
            if (failCommit) Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            else await Players.AssertRuleBrokenAsync(response, "economy.reconciliation");
            Assert.Equal(0, (await db.KingdomEconomies.AsNoTracking().SingleAsync(x => x.VillageId == id)).TreasuryCoins);
            Assert.False(await db.TreasuryIssues.AnyAsync(x => x.VillageId == id)); Assert.False(await db.TreasuryIssueDays.AnyAsync(x => x.VillageId == id));
            Assert.False(await db.MonetaryBatches.AnyAsync(x => x.VillageId == id)); Assert.False(await db.LedgerEntries.AnyAsync(x => x.VillageId == id && x.Asset == "DZK"));
        }
        finally
        {
            if (failCommit) await EconomyCommitFailure.Remove(db, trigger, "TreasuryIssues");
            else { var sql = $"DROP TRIGGER {trigger} ON \"TreasuryIssues\"; DROP FUNCTION {trigger}();"; await db.Database.ExecuteSqlRawAsync(sql); }
        }
        await Players.OkJsonAsync(await Issue(world, request)); Assert.Single(await db.TreasuryIssues.Where(x => x.VillageId == id).ToListAsync());
        Assert.Equal(60, (await db.KingdomEconomies.AsNoTracking().SingleAsync(x => x.VillageId == id)).TreasuryCoins);
    }
}
