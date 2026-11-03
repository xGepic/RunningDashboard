using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <summary>One Yearly Goal per year, keyed by its year, instead of a single row replaced each year.</summary>
    public partial class KeepYearlyGoalPerYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_YearlyGoals",
                table: "YearlyGoals");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "YearlyGoals");

            migrationBuilder.AddPrimaryKey(
                name: "PK_YearlyGoals",
                table: "YearlyGoals",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to a single goal: the latest year's, with the singleton id.
            migrationBuilder.Sql("DELETE FROM YearlyGoals WHERE Year < (SELECT MAX(Year) FROM YearlyGoals);");

            migrationBuilder.DropPrimaryKey(
                name: "PK_YearlyGoals",
                table: "YearlyGoals");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "YearlyGoals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddPrimaryKey(
                name: "PK_YearlyGoals",
                table: "YearlyGoals",
                column: "Id");
        }
    }
}
