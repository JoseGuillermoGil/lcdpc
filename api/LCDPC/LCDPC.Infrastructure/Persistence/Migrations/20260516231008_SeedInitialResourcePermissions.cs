using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LCDPC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialResourcePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "api_resources",
                columns: new[] { "Id", "Code", "Description", "Endpoint", "Method", "Name" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444441"), "auth.register.start", "Endpoint para iniciar flujo de registro y emitir OTP", "/api/v1/auth/register/start", "POST", "Iniciar registro de cliente" },
                    { new Guid("44444444-4444-4444-4444-444444444442"), "health.api.get", "Health endpoint del controlador API", "/api/health", "GET", "Health API Controller" },
                    { new Guid("44444444-4444-4444-4444-444444444443"), "health.probe.get", "Health check de infraestructura", "/health", "GET", "Health probe" }
                });

            migrationBuilder.InsertData(
                table: "role_resource_permissions",
                columns: new[] { "Id", "CanAll", "CanDelete", "CanUpdate", "CanView", "CanWrite", "CreatedAtUtc", "ResourceId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555551"), false, false, false, false, true, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444441"), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("55555555-5555-5555-5555-555555555552"), false, false, false, false, true, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444441"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("55555555-5555-5555-5555-555555555553"), false, false, false, false, true, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444441"), new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("55555555-5555-5555-5555-555555555554"), false, false, false, true, false, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444442"), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("55555555-5555-5555-5555-555555555555"), false, false, false, true, false, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444442"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("55555555-5555-5555-5555-555555555556"), false, false, false, true, false, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444442"), new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("55555555-5555-5555-5555-555555555557"), false, false, false, true, false, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444443"), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("55555555-5555-5555-5555-555555555558"), false, false, false, true, false, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444443"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("55555555-5555-5555-5555-555555555559"), false, false, false, true, false, new DateTime(2026, 5, 16, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("44444444-4444-4444-4444-444444444443"), new Guid("33333333-3333-3333-3333-333333333333") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555551"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555552"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555553"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555554"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555556"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555557"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555558"));

            migrationBuilder.DeleteData(
                table: "role_resource_permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555559"));

            migrationBuilder.DeleteData(
                table: "api_resources",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444441"));

            migrationBuilder.DeleteData(
                table: "api_resources",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444442"));

            migrationBuilder.DeleteData(
                table: "api_resources",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444443"));
        }
    }
}
