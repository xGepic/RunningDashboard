using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FillMileSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The splits per mile are in each Run's stored detail already, so no Run needs fetching from Strava again.
            // Apart from StoreMileSplits: SQLite gets the new key only once that migration has rebuilt the table.
            migrationBuilder.Sql("""
                INSERT INTO Split (RunId, Unit, "Index", Distance, MovingTime, ElapsedTime, ElevationDifference)
                SELECT Runs.Id, 'Mile', json_extract(mile.value, '$.split'), json_extract(mile.value, '$.distance'),
                       COALESCE(json_extract(mile.value, '$.moving_time'), 0), COALESCE(json_extract(mile.value, '$.elapsed_time'), 0),
                       COALESCE(json_extract(mile.value, '$.elevation_difference'), 0)
                FROM Runs, json_each(Runs.RawDetailJson, '$.splits_standard') AS mile
                WHERE Runs.RawDetailJson IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Split WHERE Unit = 'Mile';");
        }
    }
}
