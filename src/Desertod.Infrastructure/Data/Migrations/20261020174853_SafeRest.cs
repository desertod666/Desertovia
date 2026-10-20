using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SafeRest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TsarRestRequestedAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TsarRestState",
                table: "Villages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RestRequestedAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestState",
                table: "Citizens",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TsarRestRequestedAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "TsarRestState",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "RestRequestedAt",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "RestState",
                table: "Citizens");
        }
    }
}
