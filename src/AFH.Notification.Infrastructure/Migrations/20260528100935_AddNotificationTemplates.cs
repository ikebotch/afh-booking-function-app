using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AFH.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedUtc",
                table: "NotificationDispatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageSubject",
                table: "NotificationDispatches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientType",
                table: "NotificationDispatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "NotificationDispatches",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateVersion",
                table: "NotificationDispatches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubjectTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NotificationTemplates",
                columns: new[] { "Id", "BodyTemplate", "Channel", "ContentType", "CreatedBy", "CreatedUtc", "Description", "IsActive", "Name", "SubjectTemplate", "TemplateKey", "TemplateVersion", "UpdatedBy", "UpdatedUtc" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "<p>Hello,</p>\n\n<p>Your booking is now confirmed.</p>\n\n<p>\nTransaction reference: {{transactionRef}}<br>\nBooking ID: {{bookingId}}<br>\nAdviser: {{adviserName}}<br>\nMeeting type: {{meetingType}}<br>\nWhen: {{when}}<br>\n{{whereLine}}\n</p>\n\n<p>{{travelLine}}</p>\n\n<p>Manage your booking:</p>\n<ul>\n<li><a href=\"{{viewBookingUrl}}\">View booking</a></li>\n<li><a href=\"{{cancelBookingUrl}}\">Cancel booking</a></li>\n<li><a href=\"{{rescheduleBookingUrl}}\">Reschedule booking</a></li>\n</ul>\n\n<p>This is an automated AFH booking notification.</p>", "Email", "text/html", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Booking confirmed", "AFH Booking: Booking Confirmed", "booking-confirmed", "v1", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Hello {{greetingName}},\n\nYour booking has been updated: Appointment Rescheduled.\nWhen: {{whenLine}}\nAdviser: {{adviserName}}\nMeeting type: {{locationLine}}\n\n{{note}}\n{{manageBookingLinks}}\n\nThis is an automated AFH booking notification.", "Email", "text/plain", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Booking rescheduled", "AFH Booking: Appointment Rescheduled", "booking-rescheduled", "v1", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Hello {{greetingName}},\n\nYour booking has been updated: Appointment Cancelled.\nWhen: {{whenLine}}\nAdviser: {{adviserName}}\nMeeting type: {{locationLine}}\n\n{{note}}\n{{manageBookingLinks}}\n\nThis is an automated AFH booking notification.", "Email", "text/plain", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Booking cancelled", "AFH Booking: Appointment Cancelled", "booking-cancelled", "v1", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Hello,\n\nWe have placed a temporary hold on your requested booking while it is being confirmed.\n\nTransaction reference: {{transactionRef}}\nHold ID: {{holdId}}\nAdviser: {{adviserName}}\nMeeting type: {{meetingType}}\nWhen: {{when}}\nHold expires: {{holdExpires}}\n\n{{travelLine}}\n{{companyLine}}\n{{manageBookingLinks}}\n\nThis is an automated AFH booking notification.", "Email", "text/plain", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Booking hold created", "AFH Booking: Hold Created", "booking-hold", "v1", "System", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_NotificationType_CreatedUtc",
                table: "NotificationDispatches",
                columns: new[] { "NotificationType", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_OutcomeCode_CreatedUtc",
                table: "NotificationDispatches",
                columns: new[] { "OutcomeCode", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_RecipientEmail_CreatedUtc",
                table: "NotificationDispatches",
                columns: new[] { "RecipientEmail", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TemplateKey_TemplateVersion_Channel",
                schema: "dbo",
                table: "NotificationTemplates",
                columns: new[] { "TemplateKey", "TemplateVersion", "Channel" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDispatches_NotificationOutbox_NotificationOutboxId",
                table: "NotificationDispatches",
                column: "NotificationOutboxId",
                principalSchema: "dbo",
                principalTable: "NotificationOutbox",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDispatches_NotificationOutbox_NotificationOutboxId",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_NotificationType_CreatedUtc",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_OutcomeCode_CreatedUtc",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_RecipientEmail_CreatedUtc",
                table: "NotificationDispatches");

            migrationBuilder.DropTable(
                name: "NotificationTemplates",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CompletedUtc",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "MessageSubject",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "RecipientType",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "NotificationDispatches");
        }
    }
}