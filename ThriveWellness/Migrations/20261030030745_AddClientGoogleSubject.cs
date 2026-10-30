using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveWellness.Migrations
{
    /// <inheritdoc />
    public partial class AddClientGoogleSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleSubject",
                table: "Clients",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_GoogleSubject",
                table: "Clients",
                column: "GoogleSubject",
                unique: true,
                filter: "\"GoogleSubject\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_GoogleSubject",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "GoogleSubject",
                table: "Clients");
        }
    }
}
