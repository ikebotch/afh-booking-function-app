using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerWorkflowRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerWorkflowRules",
                columns: table => new
                {
                    ChangeType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerWorkflowRules", x => x.ChangeType);
                });

            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();

                INSERT INTO [dbo].[PartnerWorkflowRules] ([ChangeType], [Enabled], [CreatedUtc], [UpdatedUtc])
                SELECT seed.[ChangeType], seed.[Enabled], @now, @now
                FROM (VALUES
                    (N'Booked', CAST(0 AS bit)),
                    (N'Cancel', CAST(1 AS bit)),
                    (N'Rearrange', CAST(1 AS bit))
                ) AS seed([ChangeType], [Enabled])
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[PartnerWorkflowRules] AS existing
                    WHERE existing.[ChangeType] = seed.[ChangeType]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerWorkflowRules");
        }
    }
}
