using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingPlanWeekTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PlannedDistance",
                table: "TrainingPlanWeeks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlannedLongRun",
                table: "TrainingPlanWeeks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecoveryWeek",
                table: "TrainingPlanWeeks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RunningDays",
                table: "TrainingPlanWeeks",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedDistance",
                table: "TrainingPlanWeeks");

            migrationBuilder.DropColumn(
                name: "PlannedLongRun",
                table: "TrainingPlanWeeks");

            migrationBuilder.DropColumn(
                name: "RecoveryWeek",
                table: "TrainingPlanWeeks");

            migrationBuilder.DropColumn(
                name: "RunningDays",
                table: "TrainingPlanWeeks");
        }
    }
}
