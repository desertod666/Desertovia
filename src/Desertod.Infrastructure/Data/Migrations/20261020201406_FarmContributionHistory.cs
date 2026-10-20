using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FarmContributionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CycleFarmContributionVersion",
                table: "Citizens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FarmCrops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BedIndex = table.Column<int>(type: "integer", nullable: false),
                    Crop = table.Column<string>(type: "text", nullable: false),
                    ContributionVersion = table.Column<int>(type: "integer", nullable: false),
                    SownAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HarvestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    YieldFactor = table.Column<double>(type: "double precision", nullable: false),
                    Contributions = table.Column<string>(type: "jsonb", nullable: true),
                    Lost = table.Column<string>(type: "jsonb", nullable: true),
                    Produced = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmCrops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmCrops_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmCrops_VillageId_PlotId_Id",
                table: "FarmCrops",
                columns: new[] { "VillageId", "PlotId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmCrops");

            migrationBuilder.DropColumn(
                name: "CycleFarmContributionVersion",
                table: "Citizens");
        }
    }
}
