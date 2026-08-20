using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerWorkflowEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PartnerWorkflowRules",
                table: "PartnerWorkflowRules");

            migrationBuilder.AddColumn<string>(
                name: "PartnerKey",
                table: "PartnerWorkflowRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.AddColumn<string>(
                name: "PartnerKey",
                table: "DownstreamUpdates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PartnerWorkflowRules",
                table: "PartnerWorkflowRules",
                columns: new[] { "ChangeType", "PartnerKey" });

            migrationBuilder.CreateTable(
                name: "PartnerWorkflowEndpoints",
                columns: table => new
                {
                    PartnerKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    BookingUpdatesUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BaseUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BookingUpdatesPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ApiKeyHeaderName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IdempotencyKeyHeaderName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadFormat = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerWorkflowEndpoints", x => x.PartnerKey);
                });

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[PartnerWorkflowEndpoints]
                    WHERE [PartnerKey] = N'Default'
                )
                BEGIN
                    INSERT INTO [dbo].[PartnerWorkflowEndpoints]
                        ([PartnerKey], [DisplayName], [Enabled], [BookingUpdatesUrl], [BaseUrl], [BookingUpdatesPath],
                         [ApiKey], [ApiKeyHeaderName], [IdempotencyKeyHeaderName], [PayloadFormat], [CreatedUtc], [UpdatedUtc])
                    VALUES
                        (N'Default', N'Default Partner', CAST(0 AS bit), NULL, NULL, N'/api/booking-updates',
                         NULL, N'Authorization', N'X-Idempotency-Key', N'LegacyWrapper', SYSUTCDATETIME(), SYSUTCDATETIME());
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerWorkflowRules_PartnerKey",
                table: "PartnerWorkflowRules",
                column: "PartnerKey");

            migrationBuilder.CreateIndex(
                name: "IX_DownstreamUpdates_PartnerKey",
                table: "DownstreamUpdates",
                column: "PartnerKey");

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerWorkflowRules_PartnerWorkflowEndpoints_PartnerKey",
                table: "PartnerWorkflowRules",
                column: "PartnerKey",
                principalTable: "PartnerWorkflowEndpoints",
                principalColumn: "PartnerKey",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartnerWorkflowRules_PartnerWorkflowEndpoints_PartnerKey",
                table: "PartnerWorkflowRules");

            migrationBuilder.DropTable(
                name: "PartnerWorkflowEndpoints");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PartnerWorkflowRules",
                table: "PartnerWorkflowRules");

            migrationBuilder.DropIndex(
                name: "IX_PartnerWorkflowRules_PartnerKey",
                table: "PartnerWorkflowRules");

            migrationBuilder.DropIndex(
                name: "IX_DownstreamUpdates_PartnerKey",
                table: "DownstreamUpdates");

            migrationBuilder.DropColumn(
                name: "PartnerKey",
                table: "PartnerWorkflowRules");

            migrationBuilder.DropColumn(
                name: "PartnerKey",
                table: "DownstreamUpdates");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PartnerWorkflowRules",
                table: "PartnerWorkflowRules",
                column: "ChangeType");
        }
    }
}
