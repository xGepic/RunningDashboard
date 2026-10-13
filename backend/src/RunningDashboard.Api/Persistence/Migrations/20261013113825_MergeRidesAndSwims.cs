using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeRidesAndSwims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListedActivities",
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
                    table.PrimaryKey("PK_ListedActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListedActivities_StartUtc",
                table: "ListedActivities",
                column: "StartUtc");

            // Moved over as they are, so nothing needs fetching from Strava again. Swims kept no elevation; it is in
            // their summary.
            migrationBuilder.Sql("""
                INSERT INTO ListedActivities (Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, RawSummaryJson)
                SELECT Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, RawSummaryJson
                FROM Rides;

                INSERT INTO ListedActivities (Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, RawSummaryJson)
                SELECT Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime,
                       COALESCE(json_extract(RawSummaryJson, '$.total_elevation_gain'), 0), RawSummaryJson
                FROM Swims;
                """);

            migrationBuilder.DropTable(
                name: "Rides");

            migrationBuilder.DropTable(
                name: "Swims");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    ElapsedTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ElevationGain = table.Column<double>(type: "REAL", nullable: false),
                    MovingTime = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RawSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    SportType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Swims",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    ElapsedTime = table.Column<int>(type: "INTEGER", nullable: false),
                    MovingTime = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RawSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    SportType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Swims", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_StartUtc",
                table: "Rides",
                column: "StartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Swims_StartUtc",
                table: "Swims",
                column: "StartUtc");

            migrationBuilder.Sql("""
                INSERT INTO Rides (Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, RawSummaryJson)
                SELECT Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, RawSummaryJson
                FROM ListedActivities
                WHERE SportType <> 'Swim';

                INSERT INTO Swims (Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, RawSummaryJson)
                SELECT Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, RawSummaryJson
                FROM ListedActivities
                WHERE SportType = 'Swim';
                """);

            migrationBuilder.DropTable(
                name: "ListedActivities");
        }
    }
}
