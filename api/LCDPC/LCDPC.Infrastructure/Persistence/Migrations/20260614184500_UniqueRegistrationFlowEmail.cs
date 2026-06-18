using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LCDPC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260614184500_UniqueRegistrationFlowEmail")]
    public partial class UniqueRegistrationFlowEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ranked_flows AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY lower(trim("Email"))
                            ORDER BY
                                CASE "Status"
                                    WHEN 'active' THEN 1
                                    WHEN 'pending_profile' THEN 2
                                    WHEN 'pending_email_verification' THEN 3
                                    ELSE 4
                                END,
                                "UpdatedAtUtc" DESC,
                                "CreatedAtUtc" DESC,
                                "Id" DESC
                        ) AS row_number
                    FROM registration_flows
                )
                DELETE FROM registration_flows AS flow
                USING ranked_flows AS ranked
                WHERE flow."Id" = ranked."Id"
                  AND ranked.row_number > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_registration_flows_Email",
                table: "registration_flows");

            migrationBuilder.CreateIndex(
                name: "IX_registration_flows_Email",
                table: "registration_flows",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_registration_flows_Email",
                table: "registration_flows");

            migrationBuilder.CreateIndex(
                name: "IX_registration_flows_Email",
                table: "registration_flows",
                column: "Email");
        }
    }
}
