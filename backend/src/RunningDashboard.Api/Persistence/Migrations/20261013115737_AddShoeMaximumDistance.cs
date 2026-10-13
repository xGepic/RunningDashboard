using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunningDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShoeMaximumDistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ShoeMaximumDistance",
                table: "AthleteSettings",
                type: "REAL",
                nullable: false,
                // The Maximum Distance every Shoe had while it was fixed.
                defaultValue: 700_000.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShoeMaximumDistance",
                table: "AthleteSettings");
        }
    }
}
