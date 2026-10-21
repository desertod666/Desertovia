using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MortalRoyalCombat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoyalCombat",
                table: "Villages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CombatRulesVersion",
                table: "RaidBattles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Villages" WHERE "RoyalCombat" IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM "RaidBattles" WHERE "CombatRulesVersion" <> 0) THEN
                        RAISE EXCEPTION 'Cannot remove mortal combat rules while saved bodies or announced battles use them.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "RoyalCombat",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "CombatRulesVersion",
                table: "RaidBattles");
        }
    }
}
