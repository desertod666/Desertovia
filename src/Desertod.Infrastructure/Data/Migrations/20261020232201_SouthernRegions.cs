using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SouthernRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same origin and row width: preserve all1350 original bytes exactly, append unknown southern rows.
            migrationBuilder.Sql("""
                UPDATE "Villages" SET "Explored" = "Explored" || decode(repeat('00',900),'hex')
                WHERE octet_length("Explored") = 1350;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not silently erase land players have actually explored after the expansion.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Villages" WHERE octet_length("Explored")=2250
                    AND substring("Explored" FROM 1351) <> decode(repeat('00',900),'hex')) THEN
                    RAISE EXCEPTION 'Southern land has been explored; restore the pre-expansion backup before downgrading.';
                  END IF;
                END $$;
                UPDATE "Villages" SET "Explored"=substring("Explored" FROM 1 FOR 1350)
                WHERE octet_length("Explored")=2250;
                """);
        }
    }
}
