using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VersionedSaleTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TaxPolicyVersion",
                table: "MarketLots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LatestTaxPolicyVersion",
                table: "KingdomEconomies",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TaxPaidToday",
                table: "GameWallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "TaxRemainder",
                table: "GameWallets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TaxRevision",
                table: "GameWallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TaxableProfitToday",
                table: "GameWallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MarketLots_VillageId_Id",
                table: "MarketLots",
                columns: new[] { "VillageId", "Id" });

            migrationBuilder.CreateTable(
                name: "TaxPolicies",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Brackets = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPolicies", x => new { x.VillageId, x.Version });
                    table.ForeignKey(
                        name: "FK_TaxPolicies_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketTaxQuotes",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersion = table.Column<long>(type: "bigint", nullable: true),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedSellerTaxRevision = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    CostBasis = table.Column<long>(type: "bigint", nullable: false),
                    CostBasisKnown = table.Column<bool>(type: "boolean", nullable: false),
                    RealizedProfit = table.Column<long>(type: "bigint", nullable: true),
                    TaxableGain = table.Column<long>(type: "bigint", nullable: false),
                    TaxAmount = table.Column<long>(type: "bigint", nullable: false),
                    RemainderAfter = table.Column<int>(type: "integer", nullable: false),
                    ExemptReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TradeRequestId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTaxQuotes", x => new { x.VillageId, x.Id });
                    table.ForeignKey(
                        name: "FK_MarketTaxQuotes_MarketLots_VillageId_LotId",
                        columns: x => new { x.VillageId, x.LotId },
                        principalTable: "MarketLots",
                        principalColumns: new[] { "VillageId", "Id" });
                    table.ForeignKey(
                        name: "FK_MarketTaxQuotes_TaxPolicies_VillageId_PolicyVersion",
                        columns: x => new { x.VillageId, x.PolicyVersion },
                        principalTable: "TaxPolicies",
                        principalColumns: new[] { "VillageId", "Version" });
                    table.ForeignKey(
                        name: "FK_MarketTaxQuotes_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketTaxEvents",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersion = table.Column<long>(type: "bigint", nullable: true),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    CostBasis = table.Column<long>(type: "bigint", nullable: false),
                    CostBasisKnown = table.Column<bool>(type: "boolean", nullable: false),
                    RealizedProfit = table.Column<long>(type: "bigint", nullable: true),
                    TaxableGain = table.Column<long>(type: "bigint", nullable: false),
                    TaxAmount = table.Column<long>(type: "bigint", nullable: false),
                    TaxableProfitBefore = table.Column<long>(type: "bigint", nullable: false),
                    TaxableProfitAfter = table.Column<long>(type: "bigint", nullable: false),
                    RemainderAfter = table.Column<int>(type: "integer", nullable: false),
                    ExemptReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTaxEvents", x => new { x.VillageId, x.Id });
                    table.ForeignKey(
                        name: "FK_MarketTaxEvents_MarketTaxQuotes_VillageId_QuoteId",
                        columns: x => new { x.VillageId, x.QuoteId },
                        principalTable: "MarketTaxQuotes",
                        principalColumns: new[] { "VillageId", "Id" });
                    table.ForeignKey(
                        name: "FK_MarketTaxEvents_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketLots_VillageId_TaxPolicyVersion",
                table: "MarketLots",
                columns: new[] { "VillageId", "TaxPolicyVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTaxEvents_VillageId_LotId",
                table: "MarketTaxEvents",
                columns: new[] { "VillageId", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketTaxEvents_VillageId_QuoteId",
                table: "MarketTaxEvents",
                columns: new[] { "VillageId", "QuoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTaxEvents_VillageId_SellerAccountId_At",
                table: "MarketTaxEvents",
                columns: new[] { "VillageId", "SellerAccountId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTaxQuotes_VillageId_LotId_CreatedAt",
                table: "MarketTaxQuotes",
                columns: new[] { "VillageId", "LotId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTaxQuotes_VillageId_PolicyVersion",
                table: "MarketTaxQuotes",
                columns: new[] { "VillageId", "PolicyVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPolicies_VillageId_EffectiveAt_Version",
                table: "TaxPolicies",
                columns: new[] { "VillageId", "EffectiveAt", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPolicies_VillageId_RequestId",
                table: "TaxPolicies",
                columns: new[] { "VillageId", "RequestId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MarketLots_TaxPolicies_VillageId_TaxPolicyVersion",
                table: "MarketLots",
                columns: new[] { "VillageId", "TaxPolicyVersion" },
                principalTable: "TaxPolicies",
                principalColumns: new[] { "VillageId", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TaxPolicies") OR EXISTS (SELECT 1 FROM "MarketTaxQuotes")
                        OR EXISTS (SELECT 1 FROM "MarketTaxEvents")
                        OR EXISTS (SELECT 1 FROM "MarketLots" WHERE "TaxPolicyVersion" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "KingdomEconomies" WHERE "LatestTaxPolicyVersion" <> 0)
                        OR EXISTS (SELECT 1 FROM "GameWallets" WHERE "TaxPaidToday" <> 0 OR "TaxableProfitToday" <> 0 OR "TaxRemainder" <> 0)
                    THEN RAISE EXCEPTION 'Cannot erase fiscal policies, quotes, settlements or accumulated tax state.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_MarketLots_TaxPolicies_VillageId_TaxPolicyVersion",
                table: "MarketLots");

            migrationBuilder.DropTable(
                name: "MarketTaxEvents");

            migrationBuilder.DropTable(
                name: "MarketTaxQuotes");

            migrationBuilder.DropTable(
                name: "TaxPolicies");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MarketLots_VillageId_Id",
                table: "MarketLots");

            migrationBuilder.DropIndex(
                name: "IX_MarketLots_VillageId_TaxPolicyVersion",
                table: "MarketLots");

            migrationBuilder.DropColumn(
                name: "TaxPolicyVersion",
                table: "MarketLots");

            migrationBuilder.DropColumn(
                name: "LatestTaxPolicyVersion",
                table: "KingdomEconomies");

            migrationBuilder.DropColumn(
                name: "TaxPaidToday",
                table: "GameWallets");

            migrationBuilder.DropColumn(
                name: "TaxRemainder",
                table: "GameWallets");

            migrationBuilder.DropColumn(
                name: "TaxRevision",
                table: "GameWallets");

            migrationBuilder.DropColumn(
                name: "TaxableProfitToday",
                table: "GameWallets");
        }
    }
}
