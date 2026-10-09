using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Runs",
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
                    RelativeEffort = table.Column<double>(type: "REAL", nullable: true),
                    ShoeId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RawSummaryJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_StartUtc",
                table: "Runs",
                column: "StartUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Runs");
        }
    }
}
