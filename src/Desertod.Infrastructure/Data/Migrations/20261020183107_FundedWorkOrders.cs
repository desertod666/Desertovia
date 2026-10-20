using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FundedWorkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WorkContractsEnabled",
                table: "KingdomEconomies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CycleOwnership",
                table: "Citizens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CyclePaymentMode",
                table: "Citizens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CycleWorkOrderClaimId",
                table: "Citizens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    PayPerCycle = table.Column<long>(type: "bigint", nullable: false),
                    AvailableCycles = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCycles = table.Column<int>(type: "integer", nullable: false),
                    CompletedCycles = table.Column<int>(type: "integer", nullable: false),
                    ReservedCoins = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.UniqueConstraint("AK_WorkOrders_VillageId_Id", x => new { x.VillageId, x.Id });
                    table.ForeignKey(
                        name: "FK_WorkOrders_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PromisedPay = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderClaims_WorkOrders_VillageId_WorkOrderId",
                        columns: x => new { x.VillageId, x.WorkOrderId },
                        principalTable: "WorkOrders",
                        principalColumns: new[] { "VillageId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderClaims_CitizenId_CycleStartedAt",
                table: "WorkOrderClaims",
                columns: new[] { "CitizenId", "CycleStartedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderClaims_VillageId_AccountId_Status",
                table: "WorkOrderClaims",
                columns: new[] { "VillageId", "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderClaims_VillageId_WorkOrderId",
                table: "WorkOrderClaims",
                columns: new[] { "VillageId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_VillageId_Status_ExpiresAt",
                table: "WorkOrders",
                columns: new[] { "VillageId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderClaims");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "WorkContractsEnabled",
                table: "KingdomEconomies");

            migrationBuilder.DropColumn(
                name: "CycleOwnership",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "CyclePaymentMode",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "CycleWorkOrderClaimId",
                table: "Citizens");
        }
    }
}
