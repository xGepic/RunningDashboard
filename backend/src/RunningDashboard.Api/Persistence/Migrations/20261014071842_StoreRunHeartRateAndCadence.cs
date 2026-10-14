using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreRunHeartRateAndCadence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageHeartRate",
                table: "Runs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Cadence",
                table: "Runs",
                type: "REAL",
                nullable: true);

            // Both are in each Run's stored summary already, so no Run needs fetching from Strava again. Strava counts
            // one foot's steps; Cadence counts both.
            migrationBuilder.Sql("""
                UPDATE Runs
                SET AverageHeartRate = json_extract(RawSummaryJson, '$.average_heartrate'),
                    Cadence = json_extract(RawSummaryJson, '$.average_cadence') * 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageHeartRate",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "Cadence",
                table: "Runs");
        }
    }
}
