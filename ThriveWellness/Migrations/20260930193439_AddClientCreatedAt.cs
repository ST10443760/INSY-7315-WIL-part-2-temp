using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveWellness.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Clients",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Backfill existing clients with their earliest booking date
            // (a real signal of when they actually joined) rather than
            // leaving every pre-existing row at the column default above.
            // Clients with no bookings at all (e.g. a waitlist-only signup)
            // fall back to this migration's own run time.
            migrationBuilder.Sql("""
                UPDATE "Clients" c
                SET "CreatedAt" = COALESCE(
                    (SELECT MIN(b."BookingDate") FROM "Bookings" b WHERE b."ClientId" = c."ClientId"),
                    NOW()
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Clients");
        }
    }
}
