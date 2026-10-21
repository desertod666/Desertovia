using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectorFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DirectorReviewId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AlmightyObservations_VillageId_Id",
                table: "AlmightyObservations",
                columns: new[] { "VillageId", "Id" });

            migrationBuilder.CreateTable(
                name: "DirectorFinanceDays",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Actions = table.Column<int>(type: "integer", nullable: false),
                    Reserved = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorFinanceDays", x => new { x.VillageId, x.Day });
                    table.ForeignKey(
                        name: "FK_DirectorFinanceDays_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectorFinancePolicies",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Limits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorFinancePolicies", x => new { x.VillageId, x.Version });
                    table.ForeignKey(
                        name: "FK_DirectorFinancePolicies_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectorFinanceActions",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersion = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    DelegatingOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    Option = table.Column<string>(type: "text", nullable: false),
                    Terms = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Intent = table.Column<string>(type: "text", nullable: false),
                    Before = table.Column<string>(type: "jsonb", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorFinanceActions", x => new { x.VillageId, x.ReviewId });
                    table.ForeignKey(
                        name: "FK_DirectorFinanceActions_AlmightyObservations_VillageId_Revie~",
                        columns: x => new { x.VillageId, x.ReviewId },
                        principalTable: "AlmightyObservations",
                        principalColumns: new[] { "VillageId", "Id" });
                    table.ForeignKey(
                        name: "FK_DirectorFinanceActions_DirectorFinanceDays_VillageId_Day",
                        columns: x => new { x.VillageId, x.Day },
                        principalTable: "DirectorFinanceDays",
                        principalColumns: new[] { "VillageId", "Day" });
                    table.ForeignKey(
                        name: "FK_DirectorFinanceActions_DirectorFinancePolicies_VillageId_Po~",
                        columns: x => new { x.VillageId, x.PolicyVersion },
                        principalTable: "DirectorFinancePolicies",
                        principalColumns: new[] { "VillageId", "Version" });
                    table.ForeignKey(
                        name: "FK_DirectorFinanceActions_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_VillageId_DirectorReviewId",
                table: "LedgerEntries",
                columns: new[] { "VillageId", "DirectorReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorFinanceActions_VillageId_At",
                table: "DirectorFinanceActions",
                columns: new[] { "VillageId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorFinanceActions_VillageId_Day",
                table: "DirectorFinanceActions",
                columns: new[] { "VillageId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorFinanceActions_VillageId_PolicyVersion",
                table: "DirectorFinanceActions",
                columns: new[] { "VillageId", "PolicyVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectorFinancePolicies_VillageId_RequestId",
                table: "DirectorFinancePolicies",
                columns: new[] { "VillageId", "RequestId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_DirectorFinanceActions_VillageId_DirectorRevi~",
                table: "LedgerEntries",
                columns: new[] { "VillageId", "DirectorReviewId" },
                principalTable: "DirectorFinanceActions",
                principalColumns: new[] { "VillageId", "ReviewId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "DirectorFinancePolicies")
                        OR EXISTS (SELECT 1 FROM "DirectorFinanceDays")
                        OR EXISTS (SELECT 1 FROM "DirectorFinanceActions")
                        OR EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "DirectorReviewId" IS NOT NULL)
                    THEN RAISE EXCEPTION 'Director finance history cannot be erased; restore a pre-upgrade snapshot instead';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_DirectorFinanceActions_VillageId_DirectorRevi~",
                table: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "DirectorFinanceActions");

            migrationBuilder.DropTable(
                name: "DirectorFinanceDays");

            migrationBuilder.DropTable(
                name: "DirectorFinancePolicies");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_VillageId_DirectorReviewId",
                table: "LedgerEntries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AlmightyObservations_VillageId_Id",
                table: "AlmightyObservations");

            migrationBuilder.DropColumn(
                name: "DirectorReviewId",
                table: "LedgerEntries");
        }
    }
}
