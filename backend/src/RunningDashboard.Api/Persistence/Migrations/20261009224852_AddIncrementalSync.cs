using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncrementalSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InitialImportCompleted",
                table: "SyncStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestActivityStart",
                table: "SyncStates",
                type: "TEXT",
                nullable: true);

            // A database whose first import already finished (no cursor left, Runs stored) goes straight to
            // incremental syncs instead of walking back through the whole history once more.
            migrationBuilder.Sql("""
                UPDATE SyncStates
                SET InitialImportCompleted = 1,
                    LatestActivityStart = (SELECT MAX(StartUtc) FROM Runs)
                WHERE ImportCursor IS NULL AND EXISTS (SELECT 1 FROM Runs);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialImportCompleted",
                table: "SyncStates");

            migrationBuilder.DropColumn(
                name: "LatestActivityStart",
                table: "SyncStates");
        }
    }
}
