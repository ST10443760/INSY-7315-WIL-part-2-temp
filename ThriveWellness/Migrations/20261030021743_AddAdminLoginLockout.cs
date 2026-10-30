using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveWellness.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminLoginLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Admins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "Admins",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Admins");
        }
    }
}
