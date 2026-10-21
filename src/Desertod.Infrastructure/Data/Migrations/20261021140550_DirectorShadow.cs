using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectorShadow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Applied",
                table: "DirectorFinanceActions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "DirectorFinanceActions" WHERE NOT "Applied")
                        OR EXISTS (SELECT 1 FROM "AlmightyStates" WHERE "Mode"='ShadowEconomy')
                    THEN RAISE EXCEPTION 'Shadow director history or active mode cannot be discarded; restore a pre-upgrade snapshot instead';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "Applied",
                table: "DirectorFinanceActions");
        }
    }
}
