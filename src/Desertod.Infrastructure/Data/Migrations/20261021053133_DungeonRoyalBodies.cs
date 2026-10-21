using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DungeonRoyalBodies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CombatRulesVersion",
                table: "DungeonRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "DungeonRuns" WHERE "CombatRulesVersion" <> 0
                        OR EXISTS (SELECT 1 FROM jsonb_array_elements("Heroes") h
                            WHERE h->>'CombatClass' IS NOT NULL OR h->>'RoyalBodyCitizenId' IS NOT NULL)) THEN
                        RAISE EXCEPTION 'Cannot remove ordinary dungeon bodies while accepted calls or their saved sources use them.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "CombatRulesVersion",
                table: "DungeonRuns");
        }
    }
}
