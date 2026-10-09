using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    ReconnectRequired = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncStates");
        }
    }
}
