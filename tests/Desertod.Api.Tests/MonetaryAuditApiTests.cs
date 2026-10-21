using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class MonetaryAuditApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task<(FoundedVillage World, Guid Id, Guid Owner)> Setup(string name)
    {
        var world = await players.FoundVillageAsync(name);
        await Players.OkJsonAsync(await world.Tsar.GetAsync(world.Path + "/economy"));
        await using var db = await Db(); var village = await db.Villages.SingleAsync(x => x.Slug == world.Slug);
        (await db.GameWallets.SingleAsync(x => x.VillageId == village.Id && x.AccountId == village.OwnerAccountId)).Coins = 1000;
        await db.SaveChangesAsync(); return (world, village.Id, village.OwnerAccountId);
    }
    private static Task<System.Net.Http.HttpResponseMessage> Post(FoundedVillage world, EconomyRequest request) =>
        world.Tsar.PostAsJsonAsync(world.Path + "/economy", request);

    [Fact]
    public async Task Transfers_burns_reserve_refunds_and_retries_have_one_reconciled_receipt_and_reach_the_director()
    {
        using var frozen = factory.Clock.Freeze(); var (world, id, owner) = await Setup("Monetary receipts");
        var fund = new EconomyRequest(Guid.NewGuid(), "fund-treasury", Price: 100);
        await Players.OkJsonAsync(await Post(world, fund)); await Players.OkJsonAsync(await Post(world, fund));
        await Players.OkJsonAsync(await Post(world, new(Guid.NewGuid(), "exchange", Amount: 1)));
        await Players.OkJsonAsync(await Post(world, new(Guid.NewGuid(), "enable-work-contracts")));
        var order = new EconomyRequest(Guid.NewGuid(), "publish-work-order", TaskId: "chop-wood", Cycles: 2, Pay: 10,
            ExpiresAt: factory.Clock.GetUtcNow().AddHours(1));
        var published = await Players.OkJsonAsync(await Post(world, order));
        var orderId = Assert.Single(published.GetProperty("workOrders").EnumerateArray()).GetProperty("id").GetGuid();
        await Players.OkJsonAsync(await Post(world, new(Guid.NewGuid(), "cancel-work-order", Target: orderId)));
        await using var db = await Db();
        var batches = await db.MonetaryBatches.Where(x => x.VillageId == id).ToListAsync();
        Assert.Equal(4, batches.Count); Assert.All(batches, x => Assert.True(x.Finalized));
        Assert.Equal(0, batches.Sum(x => x.Issued)); Assert.Equal(100, batches.Sum(x => x.Withdrawn));
        Assert.Equal(140, batches.Sum(x => x.Transferred));
        Assert.All(batches, x => Assert.Equal(x.BeforeTotal + x.Issued - x.Withdrawn, x.AfterTotal));
        Assert.All(batches, x => Assert.Equal(64, x.PostingHash.Length));
        var entries = await db.LedgerEntries.Where(x => x.VillageId == id && x.Asset == "DZK").ToListAsync();
        Assert.Equal(batches.Sum(x => x.PostingCount), entries.Count);
        Assert.All(entries, x => Assert.Contains(batches, b => b.Id == x.FinancialBatchId));
        Assert.Equal(800, (await db.GameWallets.SingleAsync(x => x.VillageId == id && x.AccountId == owner)).Coins);
        Assert.Equal(100, (await db.KingdomEconomies.SingleAsync(x => x.VillageId == id)).TreasuryCoins);
        var game = factory.Services.GetRequiredService<GameService>();
        var review = (await game.BeginAlmightyReviewAsync(id, default))!;
        using var json = JsonDocument.Parse(review.Snapshot);
        var evidence = json.RootElement.GetProperty("economy").GetProperty("analysis").GetProperty("moneyFlows").GetProperty("validated");
        Assert.Equal(100, evidence.GetProperty("totals").GetProperty("withdrawn").GetDecimal());
        Assert.True(evidence.GetProperty("currentHoldingsMatchLastAudit").GetBoolean());
        Assert.False(evidence.GetProperty("currentActionNotYetFinalized").GetBoolean());
        Assert.DoesNotContain(owner.ToString(), evidence.GetRawText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unjournaled_balance_change_even_with_equal_global_total_rolls_back_the_entire_request(bool preserveTotal)
    {
        using var frozen = factory.Clock.Freeze(); var (world, id, owner) = await Setup("Reconciliation rollback");
        var request = new EconomyRequest(Guid.NewGuid(), "fund-treasury", Price: 100);
        await using var db = await Db();
        var name = "change_balance_" + Guid.NewGuid().ToString("N");
        // Generated identifiers and GUID literals only; this trigger belongs to this throwaway fixture.
        var treasury = preserveTotal ? $"UPDATE \"KingdomEconomies\" SET \"TreasuryCoins\"=\"TreasuryCoins\"-1 WHERE \"VillageId\"='{id}'::uuid;" : "";
        var sql = $"CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"OperationId\"='{request.RequestId}'::uuid AND NEW.\"Asset\"='DZK' AND NEW.\"Location\"='wallet' THEN UPDATE \"GameWallets\" SET \"Coins\"=\"Coins\"+1 WHERE \"VillageId\"='{id}'::uuid AND \"AccountId\"='{owner}'::uuid; {treasury} END IF; RETURN NEW; END; $$; CREATE TRIGGER {name} AFTER INSERT ON \"LedgerEntries\" FOR EACH ROW EXECUTE FUNCTION {name}();";
        await db.Database.ExecuteSqlRawAsync(sql);
        try
        {
            await Players.AssertRuleBrokenAsync(await Post(world, request), "economy.reconciliation");
            Assert.Equal(1000, (await db.GameWallets.AsNoTracking().SingleAsync(x => x.VillageId == id && x.AccountId == owner)).Coins);
            Assert.Equal(0, (await db.KingdomEconomies.AsNoTracking().SingleAsync(x => x.VillageId == id)).TreasuryCoins);
            Assert.Empty(await db.MonetaryBatches.Where(x => x.VillageId == id).ToListAsync());
            Assert.False(await db.EconomyReceipts.AnyAsync(x => x.VillageId == id && x.RequestId == request.RequestId));
            Assert.False(await db.LedgerEntries.AnyAsync(x => x.VillageId == id && x.OperationId == request.RequestId));
        }
        finally
        {
            var dropSql = $"DROP TRIGGER {name} ON \"LedgerEntries\"; DROP FUNCTION {name}();";
            await db.Database.ExecuteSqlRawAsync(dropSql);
        }
        await Players.OkJsonAsync(await Post(world, request));
        Assert.Single(await db.MonetaryBatches.Where(x => x.VillageId == id).ToListAsync());
    }

    [Fact]
    public async Task External_equal_total_adjustment_is_reported_as_a_changed_anchor_not_as_proven_income()
    {
        using var frozen = factory.Clock.Freeze(); var (world, id, owner) = await Setup("Changed monetary anchor");
        await Players.OkJsonAsync(await Post(world, new(Guid.NewGuid(), "fund-treasury", Price: 100)));
        await using var db = await Db();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"GameWallets\" SET \"Coins\"=\"Coins\"+1 WHERE \"VillageId\"={id} AND \"AccountId\"={owner}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"KingdomEconomies\" SET \"TreasuryCoins\"=\"TreasuryCoins\"-1 WHERE \"VillageId\"={id}");
        // A subsequent valid operation must not hide the earlier unjournaled transfer.
        await Players.OkJsonAsync(await Post(world, new(Guid.NewGuid(), "fund-treasury", Price: 1)));
        var review = (await factory.Services.GetRequiredService<GameService>().BeginAlmightyReviewAsync(id, default))!;
        using var json = JsonDocument.Parse(review.Snapshot);
        var evidence = json.RootElement.GetProperty("economy").GetProperty("analysis").GetProperty("moneyFlows").GetProperty("validated");
        Assert.True(evidence.GetProperty("currentHoldingsMatchLastAudit").GetBoolean());
        Assert.Equal(1, evidence.GetProperty("anchorChanges").GetInt32());
        Assert.Equal(0, evidence.GetProperty("unexplainedAnchorNet").GetDecimal());
        Assert.True(evidence.GetProperty("anchorsAreNotIssuance").GetBoolean());
        Assert.Equal(0, evidence.GetProperty("totals").GetProperty("issued").GetDecimal());
        Assert.Equal(2, await db.MonetaryBatches.CountAsync(x => x.VillageId == id));
    }
}
