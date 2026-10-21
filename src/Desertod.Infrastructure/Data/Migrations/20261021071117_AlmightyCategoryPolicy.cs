using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlmightyCategoryPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "DisabledKinds",
                table: "AlmightyStates",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<long>(
                name: "PolicyVersion",
                table: "AlmightyStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AlmightyPolicyChanges",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExpectedPolicyVersion = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersion = table.Column<long>(type: "bigint", nullable: false),
                    Changed = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisabledKinds = table.Column<string[]>(type: "text[]", nullable: false),
                    SupersededProposals = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlmightyPolicyChanges", x => new { x.VillageId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_AlmightyPolicyChanges_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlmightyPolicyChanges_VillageId_At_RequestId",
                table: "AlmightyPolicyChanges",
                columns: new[] { "VillageId", "At", "RequestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AlmightyStates" WHERE cardinality("DisabledKinds") > 0 OR "PolicyVersion" <> 0)
                        OR EXISTS (SELECT 1 FROM "AlmightyPolicyChanges") THEN
                        RAISE EXCEPTION 'Cannot erase director stops, policy revisions or durable request acknowledgements.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "AlmightyPolicyChanges");

            migrationBuilder.DropColumn(
                name: "DisabledKinds",
                table: "AlmightyStates");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "AlmightyStates");
        }
    }
}
