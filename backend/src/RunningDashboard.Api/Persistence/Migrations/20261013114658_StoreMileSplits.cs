using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreMileSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Split",
                table: "Split");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Split",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "Kilometre");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Split",
                table: "Split",
                columns: new[] { "RunId", "Unit", "Index" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Split",
                table: "Split");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Split");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Split",
                table: "Split",
                columns: new[] { "RunId", "Index" });
        }
    }
}
