using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TreasuryFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreasuryFundingPolicies",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DailyIssueLimit = table.Column<long>(type: "bigint", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryFundingPolicies", x => new { x.VillageId, x.Version });
                    table.ForeignKey(
                        name: "FK_TreasuryFundingPolicies_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreasuryIssueDays",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Issued = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryIssueDays", x => new { x.VillageId, x.Day });
                    table.ForeignKey(
                        name: "FK_TreasuryIssueDays_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreasuryIssues",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PolicyVersion = table.Column<long>(type: "bigint", nullable: false),
                    FinancialBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    IssuedTotalAfter = table.Column<long>(type: "bigint", nullable: false),
                    RemainingAtIssue = table.Column<long>(type: "bigint", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryIssues", x => new { x.VillageId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_TreasuryIssues_MonetaryBatches_VillageId_FinancialBatchId",
                        columns: x => new { x.VillageId, x.FinancialBatchId },
                        principalTable: "MonetaryBatches",
                        principalColumns: new[] { "VillageId", "Id" });
                    table.ForeignKey(
                        name: "FK_TreasuryIssues_TreasuryFundingPolicies_VillageId_PolicyVers~",
                        columns: x => new { x.VillageId, x.PolicyVersion },
                        principalTable: "TreasuryFundingPolicies",
                        principalColumns: new[] { "VillageId", "Version" });
                    table.ForeignKey(
                        name: "FK_TreasuryIssues_TreasuryIssueDays_VillageId_Day",
                        columns: x => new { x.VillageId, x.Day },
                        principalTable: "TreasuryIssueDays",
                        principalColumns: new[] { "VillageId", "Day" });
                    table.ForeignKey(
                        name: "FK_TreasuryIssues_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryFundingPolicies_VillageId_EffectiveAt_Version",
                table: "TreasuryFundingPolicies",
                columns: new[] { "VillageId", "EffectiveAt", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryFundingPolicies_VillageId_RequestId",
                table: "TreasuryFundingPolicies",
                columns: new[] { "VillageId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryIssues_VillageId_At",
                table: "TreasuryIssues",
                columns: new[] { "VillageId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryIssues_VillageId_Day",
                table: "TreasuryIssues",
                columns: new[] { "VillageId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryIssues_VillageId_FinancialBatchId",
                table: "TreasuryIssues",
                columns: new[] { "VillageId", "FinancialBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryIssues_VillageId_PolicyVersion",
                table: "TreasuryIssues",
                columns: new[] { "VillageId", "PolicyVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TreasuryFundingPolicies") OR EXISTS (SELECT 1 FROM "TreasuryIssues")
                        OR EXISTS (SELECT 1 FROM "TreasuryIssueDays")
                    THEN RAISE EXCEPTION 'Cannot erase declared treasury funding or issuance history.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "TreasuryIssues");

            migrationBuilder.DropTable(
                name: "TreasuryFundingPolicies");

            migrationBuilder.DropTable(
                name: "TreasuryIssueDays");
        }
    }
}
