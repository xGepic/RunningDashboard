using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreRunStartLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "StartLatitude",
                table: "Runs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StartLongitude",
                table: "Runs",
                type: "REAL",
                nullable: true);

            // From each Run's stored summary, so no Run needs fetching from Strava again; a Run without GPS has an empty list.
            migrationBuilder.Sql("""
                UPDATE Runs
                SET StartLatitude = json_extract(RawSummaryJson, '$.start_latlng[0]'),
                    StartLongitude = json_extract(RawSummaryJson, '$.start_latlng[1]');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartLatitude",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "StartLongitude",
                table: "Runs");
        }
    }
}
