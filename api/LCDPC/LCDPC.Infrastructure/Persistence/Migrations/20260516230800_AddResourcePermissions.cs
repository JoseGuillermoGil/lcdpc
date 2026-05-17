using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCDPC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourcePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scope",
                table: "roles");

            migrationBuilder.CreateTable(
                name: "api_resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_resources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_resource_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanView = table.Column<bool>(type: "boolean", nullable: false),
                    CanWrite = table.Column<bool>(type: "boolean", nullable: false),
                    CanUpdate = table.Column<bool>(type: "boolean", nullable: false),
                    CanDelete = table.Column<bool>(type: "boolean", nullable: false),
                    CanAll = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_resource_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_resource_permissions_api_resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "api_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_resource_permissions_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_resources_Code",
                table: "api_resources",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_resources_Endpoint_Method",
                table: "api_resources",
                columns: new[] { "Endpoint", "Method" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_resource_permissions_ResourceId",
                table: "role_resource_permissions",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_role_resource_permissions_RoleId_ResourceId",
                table: "role_resource_permissions",
                columns: new[] { "RoleId", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_resource_permissions");

            migrationBuilder.DropTable(
                name: "api_resources");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "roles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "Scope",
                value: "self");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "Scope",
                value: "sede");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "Scope",
                value: "global");
        }
    }
}
