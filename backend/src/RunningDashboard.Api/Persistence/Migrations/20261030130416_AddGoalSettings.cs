using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both goals stay shown in a settings row stored before, as they were.
            migrationBuilder.AddColumn<bool>(
                name: "ShowWeeklyTimeGoal",
                table: "AthleteSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowYearlyGoal",
                table: "AthleteSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowWeeklyTimeGoal",
                table: "AthleteSettings");

            migrationBuilder.DropColumn(
                name: "ShowYearlyGoal",
                table: "AthleteSettings");
        }
    }
}
