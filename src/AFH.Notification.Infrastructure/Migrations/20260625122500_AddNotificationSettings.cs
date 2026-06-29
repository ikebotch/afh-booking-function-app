using System;
using AFH.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Notification.Infrastructure.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20260625122500_AddNotificationSettings")]
public partial class AddNotificationSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationSettings",
            schema: "dbo",
            columns: table => new
            {
                Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IsSecret = table.Column<bool>(type: "bit", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationSettings", x => x.Key);
            });

        migrationBuilder.CreateIndex(
            name: "IX_NotificationSettings_Category",
            schema: "dbo",
            table: "NotificationSettings",
            column: "Category");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NotificationSettings",
            schema: "dbo");
    }
}
