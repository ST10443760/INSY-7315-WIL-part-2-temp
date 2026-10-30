using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ThriveWellness.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardsAndNoShow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NoShow",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    BadgeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IconKey = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.BadgeId);
                });

            migrationBuilder.CreateTable(
                name: "ClientBadges",
                columns: table => new
                {
                    ClientBadgeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    BadgeId = table.Column<int>(type: "integer", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientBadges", x => x.ClientBadgeId);
                    table.ForeignKey(
                        name: "FK_ClientBadges_Badges_BadgeId",
                        column: x => x.BadgeId,
                        principalTable: "Badges",
                        principalColumn: "BadgeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientBadges_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "BadgeId", "Code", "Description", "IconKey", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "first-class", "Attended your first class.", "star", "First Class", 1 },
                    { 2, "getting-going", "Attended 5 classes.", "leaf", "Getting Going", 2 },
                    { 3, "committed", "Attended 10 classes.", "medal", "Committed", 3 },
                    { 4, "dedicated", "Attended 25 classes.", "trophy", "Dedicated", 4 },
                    { 5, "three-week-streak", "Attended a class in 3 weeks in a row.", "flame", "Three Week Streak", 5 },
                    { 6, "eight-week-streak", "Attended a class in 8 weeks in a row.", "bolt", "Eight Week Streak", 6 },
                    { 7, "both-studios", "Attended a class at both of our studios.", "pin", "Both Studios", 7 },
                    { 8, "early-bird", "Attended 3 classes that start before 09:00.", "sunrise", "Early Bird", 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Badges_Code",
                table: "Badges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientBadges_BadgeId",
                table: "ClientBadges",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBadges_ClientId_BadgeId",
                table: "ClientBadges",
                columns: new[] { "ClientId", "BadgeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientBadges");

            migrationBuilder.DropTable(
                name: "Badges");

            migrationBuilder.DropColumn(
                name: "NoShow",
                table: "Bookings");
        }
    }
}
