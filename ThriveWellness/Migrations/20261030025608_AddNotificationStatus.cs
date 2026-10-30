using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveWellness.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Notifications",
                type: "text",
                nullable: false,
                defaultValue: "Sent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Notifications");
        }
    }
}
