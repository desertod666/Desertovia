using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MonetaryConservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinancialBatchId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MonetaryBatches",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Finalized = table.Column<bool>(type: "boolean", nullable: false),
                    BeforeTotal = table.Column<decimal>(type: "numeric(28,0)", precision: 28, scale: 0, nullable: false),
                    AfterTotal = table.Column<decimal>(type: "numeric(28,0)", precision: 28, scale: 0, nullable: false),
                    Issued = table.Column<decimal>(type: "numeric(28,0)", precision: 28, scale: 0, nullable: false),
                    Withdrawn = table.Column<decimal>(type: "numeric(28,0)", precision: 28, scale: 0, nullable: false),
                    Transferred = table.Column<decimal>(type: "numeric(28,0)", precision: 28, scale: 0, nullable: false),
                    PostingCount = table.Column<int>(type: "integer", nullable: false),
                    BeforeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AfterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PostingHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Operations = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonetaryBatches", x => new { x.VillageId, x.Id });
                    table.ForeignKey(
                        name: "FK_MonetaryBatches_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_VillageId_FinancialBatchId",
                table: "LedgerEntries",
                columns: new[] { "VillageId", "FinancialBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonetaryBatches_VillageId_At",
                table: "MonetaryBatches",
                columns: new[] { "VillageId", "At" });

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_MonetaryBatches_VillageId_FinancialBatchId",
                table: "LedgerEntries",
                columns: new[] { "VillageId", "FinancialBatchId" },
                principalTable: "MonetaryBatches",
                principalColumns: new[] { "VillageId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "MonetaryBatches")
                        OR EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "FinancialBatchId" IS NOT NULL)
                    THEN RAISE EXCEPTION 'Cannot erase monetary reconciliation receipts or their postings.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_MonetaryBatches_VillageId_FinancialBatchId",
                table: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "MonetaryBatches");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_VillageId_FinancialBatchId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "FinancialBatchId",
                table: "LedgerEntries");
        }
    }
}
