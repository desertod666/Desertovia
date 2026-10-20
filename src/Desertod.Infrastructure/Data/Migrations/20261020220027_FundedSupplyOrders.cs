using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FundedSupplyOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TradeReserves",
                table: "KingdomEconomies",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplyOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestedAmount = table.Column<int>(type: "integer", nullable: false),
                    RemainingAmount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredAmount = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<long>(type: "bigint", nullable: false),
                    ReservedCoins = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyOrders", x => x.Id);
                    table.UniqueConstraint("AK_SupplyOrders_VillageId_Id", x => new { x.VillageId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupplyOrders_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplyDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    PaidCoins = table.Column<long>(type: "bigint", nullable: false),
                    CostBasis = table.Column<long>(type: "bigint", nullable: false),
                    CostBasisKnown = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyDeliveries_SupplyOrders_VillageId_SupplyOrderId",
                        columns: x => new { x.VillageId, x.SupplyOrderId },
                        principalTable: "SupplyOrders",
                        principalColumns: new[] { "VillageId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyDeliveries_VillageId_SupplierAccountId_At",
                table: "SupplyDeliveries",
                columns: new[] { "VillageId", "SupplierAccountId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyDeliveries_VillageId_SupplyOrderId",
                table: "SupplyDeliveries",
                columns: new[] { "VillageId", "SupplyOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyOrders_VillageId_Status_ExpiresAt",
                table: "SupplyOrders",
                columns: new[] { "VillageId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplyDeliveries");

            migrationBuilder.DropTable(
                name: "SupplyOrders");

            migrationBuilder.DropColumn(
                name: "TradeReserves",
                table: "KingdomEconomies");
        }
    }
}
