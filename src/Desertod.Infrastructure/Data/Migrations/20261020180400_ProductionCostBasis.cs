using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionCostBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CostBasisKnown",
                table: "MarketLots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "CycleInputCostBasis",
                table: "Citizens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "CycleInputCostBasisKnown",
                table: "Citizens",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostBasisKnown",
                table: "MarketLots");

            migrationBuilder.DropColumn(
                name: "CycleInputCostBasis",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "CycleInputCostBasisKnown",
                table: "Citizens");
        }
    }
}
