using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreFitnessData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "GradeAdjustedSpeed",
                table: "Split",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HeartRate",
                table: "Split",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Trainer",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // All of it is in each Run's stored summary and detail already, so no Run needs fetching from Strava again.
            migrationBuilder.Sql("""
                UPDATE Runs SET Trainer = COALESCE(json_extract(RawSummaryJson, '$.trainer'), 0);

                UPDATE Split
                SET HeartRate = (
                        SELECT json_extract(s.value, '$.average_heartrate')
                        FROM Runs, json_each(Runs.RawDetailJson, CASE Split.Unit WHEN 'Mile' THEN '$.splits_standard' ELSE '$.splits_metric' END) AS s
                        WHERE Runs.Id = Split.RunId AND json_extract(s.value, '$.split') = Split."Index"),
                    GradeAdjustedSpeed = (
                        SELECT json_extract(s.value, '$.average_grade_adjusted_speed')
                        FROM Runs, json_each(Runs.RawDetailJson, CASE Split.Unit WHEN 'Mile' THEN '$.splits_standard' ELSE '$.splits_metric' END) AS s
                        WHERE Runs.Id = Split.RunId AND json_extract(s.value, '$.split') = Split."Index");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradeAdjustedSpeed",
                table: "Split");

            migrationBuilder.DropColumn(
                name: "HeartRate",
                table: "Split");

            migrationBuilder.DropColumn(
                name: "Trainer",
                table: "Runs");
        }
    }
}
