using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "NotificationOutbox",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceApplication = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QueueMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_IdempotencyKey",
                schema: "dbo",
                table: "NotificationOutbox",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status_CreatedUtc",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status_LockedUntilUtc",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "Status", "LockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status_NextAttemptUtc",
                schema: "dbo",
                table: "NotificationOutbox",
                columns: new[] { "Status", "NextAttemptUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationOutbox",
                schema: "dbo");
        }
    }
}
