using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desertod.Domain.Economy;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    private sealed class MonetaryHolding
    {
        public required string Location { get; set; }
        public Guid? AccountId { get; set; }
        public long Amount { get; set; }
    }

    private sealed class MonetaryAnchors
    {
        public int Changes { get; set; }
        public decimal NetChange { get; set; }
    }

    private static Task<MonetaryAnchors> ObserveMonetaryAnchorsAsync(DesertodDbContext db, Guid village,
        DateTimeOffset start, DateTimeOffset now, CancellationToken ct) => db.Database.SqlQuery<MonetaryAnchors>($"""
            WITH recent AS (
                SELECT "At","Id","BeforeHash","AfterHash","BeforeTotal","AfterTotal" FROM "MonetaryBatches"
                WHERE "VillageId"={village} AND "Finalized" AND "At">={start} AND "At"<={now}
            ), prior AS (
                SELECT "At","Id","BeforeHash","AfterHash","BeforeTotal","AfterTotal" FROM "MonetaryBatches"
                WHERE "VillageId"={village} AND "Finalized" AND "At"<{start}
                ORDER BY "At" DESC,"Id" DESC LIMIT 1
            ), linked AS (
                SELECT *,lag("AfterHash") OVER (ORDER BY "At","Id") AS previous_hash,
                    lag("AfterTotal") OVER (ORDER BY "At","Id") AS previous_total
                FROM (SELECT * FROM recent UNION ALL SELECT * FROM prior) history
            )
            SELECT count(*) FILTER (WHERE previous_hash IS NOT NULL AND previous_hash<>"BeforeHash")::int AS "Changes",
                COALESCE(sum("BeforeTotal"-previous_total) FILTER (WHERE previous_hash IS NOT NULL AND previous_hash<>"BeforeHash"),0)::numeric AS "NetChange"
            FROM linked WHERE "At">={start}
            """).SingleAsync(ct);

    private static async Task<Dictionary<MonetaryPosition, long>> SnapshotMoneyAsync(DesertodDbContext db, Guid village, CancellationToken ct)
    {
        // One query covers all owners and reserves. Accepted promises are part of a reserve, not additional money.
        var rows = await db.Database.SqlQuery<MonetaryHolding>($"""
            SELECT 'wallet' AS "Location", "AccountId", "Coins" AS "Amount" FROM "GameWallets" WHERE "VillageId"={village}
            UNION ALL SELECT 'treasury', NULL::uuid, "TreasuryCoins" FROM "KingdomEconomies" WHERE "VillageId"={village}
            UNION ALL SELECT 'work-reserve:' || "Id"::text, NULL::uuid, "ReservedCoins" FROM "WorkOrders" WHERE "VillageId"={village}
            UNION ALL SELECT 'supply-order:' || "Id"::text, NULL::uuid, "ReservedCoins" FROM "SupplyOrders" WHERE "VillageId"={village}
            """).ToListAsync(ct);
        return rows.ToDictionary(x => MonetaryPosition.From(x.Location, x.AccountId), x => x.Amount);
    }

    private static string MonetaryHash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string HoldingHash(IReadOnlyDictionary<MonetaryPosition, long> positions) => MonetaryHash(positions.Where(x => x.Value != 0)
        .OrderBy(x => x.Key.Location, StringComparer.Ordinal).ThenBy(x => x.Key.AccountId)
        .Select(x => new { x.Key.Location, x.Key.AccountId, Amount = x.Value }).ToArray());

    private static async Task FinishMonetaryAuditAsync(DesertodDbContext db, Guid village,
        IReadOnlyDictionary<MonetaryPosition, long> before, CancellationToken ct)
    {
        var after = await SnapshotMoneyAsync(db, village, ct);
        var postings = await db.LedgerEntries.AsNoTracking()
            .Where(x => x.VillageId == village && x.FinancialBatchId == db.FinancialBatchId).OrderBy(x => x.Id).ToListAsync(ct);
        var operations = MonetaryAudit.Reconcile(before, after, postings);
        if (postings.Count == 0) return; // Reads and non-monetary actions do not generate empty audit history.
        var batch = await db.MonetaryBatches.SingleAsync(x => x.VillageId == village && x.Id == db.FinancialBatchId, ct);
        if (batch.Finalized) throw MonetaryAudit.Invalid();
        batch.BeforeTotal = before.Values.Sum(x => (decimal)x); batch.AfterTotal = after.Values.Sum(x => (decimal)x);
        batch.BeforeHash = HoldingHash(before); batch.AfterHash = HoldingHash(after);
        batch.PostingHash = MonetaryHash(postings.Select(x => new { x.Id, x.OperationId, x.AccountId, x.Operation, x.Location, x.Delta }).ToArray());
        batch.Operations = operations.ToList(); batch.Issued = operations.Sum(x => x.Issued);
        batch.Withdrawn = operations.Sum(x => x.Withdrawn); batch.Transferred = operations.Sum(x => x.Transferred);
        batch.PostingCount = postings.Count; batch.Finalized = true;
        // The caller saves this receipt with its stock journal, then commits the original transaction.
    }
}
