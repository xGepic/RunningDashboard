using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every Running Day so far, a weekday number, becomes one with Run Type Easy (0), in the same order.
            migrationBuilder.Sql(
                """
                UPDATE "TrainingPlanWeeks"
                SET "RunningDays" = (
                    SELECT json_group_array(json_object('Day', "value", 'RunType', 0) ORDER BY "key")
                    FROM json_each("TrainingPlanWeeks"."RunningDays"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to plain weekday numbers; the Run Types are lost.
            migrationBuilder.Sql(
                """
                UPDATE "TrainingPlanWeeks"
                SET "RunningDays" = (
                    SELECT json_group_array(json_extract("value", '$.Day') ORDER BY "key")
                    FROM json_each("TrainingPlanWeeks"."RunningDays"));
                """);
        }
    }
}
