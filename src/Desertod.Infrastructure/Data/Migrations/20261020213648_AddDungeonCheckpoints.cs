using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDungeonCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckpointJson",
                table: "DungeonRuns",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckpointJson",
                table: "DungeonRuns");
        }
    }
}
