using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCDPC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthTraceAndLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardingStatus",
                table: "users",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "registration_flows",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "OnboardingStatus",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "registration_flows");
        }
    }
}
