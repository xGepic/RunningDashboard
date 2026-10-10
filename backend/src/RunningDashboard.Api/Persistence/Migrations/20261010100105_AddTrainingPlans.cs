using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingPlans",
                columns: table => new
                {
                    GoalRaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<int>(type: "INTEGER", nullable: false),
                    Phases = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPlans", x => x.GoalRaceId);
                    table.ForeignKey(
                        name: "FK_TrainingPlans_GoalRaces_GoalRaceId",
                        column: x => x.GoalRaceId,
                        principalTable: "GoalRaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingPlanWeeks",
                columns: table => new
                {
                    WeeksToRace = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalRaceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPlanWeeks", x => new { x.GoalRaceId, x.WeeksToRace });
                    table.ForeignKey(
                        name: "FK_TrainingPlanWeeks_TrainingPlans_GoalRaceId",
                        column: x => x.GoalRaceId,
                        principalTable: "TrainingPlans",
                        principalColumn: "GoalRaceId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingPlanWeeks");

            migrationBuilder.DropTable(
                name: "TrainingPlans");
        }
    }
}
