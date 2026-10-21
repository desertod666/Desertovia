using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmbushEnemy",
                table: "DungeonRuns",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "DungeonRuns",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Group");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkCitizenId",
                table: "DungeonRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WorkStartedAt",
                table: "DungeonRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkTaskId",
                table: "DungeonRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CycleAmbushChecked",
                table: "Citizens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CycleEncounterReportJson",
                table: "Citizens",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CycleEncounterRunId",
                table: "Citizens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CycleEncounterSeed",
                table: "Citizens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CycleEncounterVersion",
                table: "Citizens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DungeonRuns_WorkCitizenId_WorkStartedAt",
                table: "DungeonRuns",
                columns: new[] { "WorkCitizenId", "WorkStartedAt" },
                unique: true,
                filter: "\"Kind\" = 'Ambush'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "DungeonRuns" WHERE "Kind"='Ambush')
                     OR EXISTS (SELECT 1 FROM "Citizens" WHERE "CycleEncounterVersion">0 AND NOT "CycleSettled") THEN
                    RAISE EXCEPTION 'Work encounters or accepted encounter cycles exist; restore the pre-feature backup instead of dropping their state';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_DungeonRuns_WorkCitizenId_WorkStartedAt",
                table: "DungeonRuns");

            migrationBuilder.DropColumn(
                name: "AmbushEnemy",
                table: "DungeonRuns");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "DungeonRuns");

            migrationBuilder.DropColumn(
                name: "WorkCitizenId",
                table: "DungeonRuns");

            migrationBuilder.DropColumn(
                name: "WorkStartedAt",
                table: "DungeonRuns");

            migrationBuilder.DropColumn(
                name: "WorkTaskId",
                table: "DungeonRuns");

            migrationBuilder.DropColumn(
                name: "CycleAmbushChecked",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "CycleEncounterReportJson",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "CycleEncounterRunId",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "CycleEncounterSeed",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "CycleEncounterVersion",
                table: "Citizens");
        }
    }
}
