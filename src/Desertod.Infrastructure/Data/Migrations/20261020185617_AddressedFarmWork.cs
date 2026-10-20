using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddressedFarmWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CycleFarmTargets",
                table: "Citizens",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CycleFarmTargets",
                table: "Citizens");
        }
    }
}
