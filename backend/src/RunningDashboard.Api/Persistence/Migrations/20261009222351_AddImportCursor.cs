using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ImportCursor",
                table: "SyncStates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportCursor",
                table: "SyncStates");
        }
    }
}
