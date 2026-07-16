using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260716120000_AddAdviserSkillProjections")]
    public partial class AddAdviserSkillProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdviserSkillProjections",
                columns: table => new
                {
                    AdviserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SkillCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SkillLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserSkillProjections", x => new { x.AdviserId, x.SkillCode });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserSkillProjections_AdviserId_IsActive",
                table: "AdviserSkillProjections",
                columns: new[] { "AdviserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserSkillProjections_LastSyncedUtc",
                table: "AdviserSkillProjections",
                column: "LastSyncedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdviserSkillProjections_SkillCode_IsActive",
                table: "AdviserSkillProjections",
                columns: new[] { "SkillCode", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviserSkillProjections");
        }
    }
}
