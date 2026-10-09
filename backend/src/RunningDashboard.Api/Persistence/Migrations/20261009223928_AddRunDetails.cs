using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DetailFetchedAt",
                table: "Runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawDetailJson",
                table: "Runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BestEffort",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    ElapsedTime = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BestEffort", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BestEffort_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Split",
                columns: table => new
                {
                    RunId = table.Column<long>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    MovingTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ElapsedTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ElevationDifference = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Split", x => new { x.RunId, x.Index });
                    table.ForeignKey(
                        name: "FK_Split_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BestEffort_RunId",
                table: "BestEffort",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BestEffort");

            migrationBuilder.DropTable(
                name: "Split");

            migrationBuilder.DropColumn(
                name: "DetailFetchedAt",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "RawDetailJson",
                table: "Runs");
        }
    }
}
