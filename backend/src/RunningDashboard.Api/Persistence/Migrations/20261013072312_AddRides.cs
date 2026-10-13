using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SportType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    MovingTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ElapsedTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ElevationGain = table.Column<double>(type: "REAL", nullable: false),
                    RawSummaryJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rides", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_StartUtc",
                table: "Rides",
                column: "StartUtc");

            // The first import skipped every Ride. Walking back through the whole history once more stores them;
            // the Runs it meets again are just updated.
            migrationBuilder.Sql("""
                UPDATE SyncStates
                SET InitialImportCompleted = 0,
                    ImportCursor = NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rides");
        }
    }
}
