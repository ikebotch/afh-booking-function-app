using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDispatchAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "NotificationDispatches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotificationOutboxId",
                table: "NotificationDispatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationType",
                table: "NotificationDispatches",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "NotificationDispatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceApplication",
                table: "NotificationDispatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "NotificationDispatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_NotificationOutboxId",
                table: "NotificationDispatches",
                column: "NotificationOutboxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_NotificationOutboxId",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "NotificationOutboxId",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "NotificationType",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "SourceApplication",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "NotificationDispatches");
        }
    }
}
