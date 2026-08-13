using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AFH.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarGovernanceNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NotificationTemplates",
                columns: new[] { "Id", "BodyTemplate", "Channel", "ContentType", "CreatedBy", "CreatedUtc", "Description", "IsActive", "Name", "SubjectTemplate", "TemplateKey", "TemplateVersion", "UpdatedBy", "UpdatedUtc" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Hello,\n\nA manual Outlook calendar change was detected and corrected.\n\nTransaction reference: {{transactionRef}}\nBooking ID: {{bookingId}}\nAdviser: {{adviserName}}\nMeeting type: {{meetingType}}\nWhen: {{when}}\nProvider event ID: {{providerEventId}}\nCorrection: {{correctionReason}}\n\nThis is an automated AFH booking notification.", "Email", "text/plain", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Calendar event corrected", "AFH Booking: Calendar Event Corrected", "calendar-event-corrected", "v1", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "Hello,\n\nA manual Outlook calendar change was detected but could not be corrected automatically.\n\nTransaction reference: {{transactionRef}}\nBooking ID: {{bookingId}}\nAdviser: {{adviserName}}\nMeeting type: {{meetingType}}\nWhen: {{when}}\nProvider event ID: {{providerEventId}}\nReason: {{correctionReason}}\n\nOperations should review this booking and reconcile the Outlook event.\n\nThis is an automated AFH booking notification.", "Email", "text/plain", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Calendar event correction failed", "AFH Booking: Calendar Reconciliation Required", "calendar-event-correction-failed", "v1", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "A manual Outlook calendar change was detected and corrected.\n\nBooking ID: {{bookingId}}\nTransaction reference: {{transactionRef}}\nAdviser: {{adviserName}}\nWhen: {{when}}\nMeeting type: {{meetingType}}\nProvider event ID: {{providerEventId}}\nCorrection: {{correctionReason}}\n\nNo booking lifecycle event was created for this correction.", "Email", "text/plain", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Calendar event corrected - adviser", "AFH Booking: Calendar Event Corrected", "calendar-event-corrected-adviser", "v1", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "A manual Outlook calendar change was detected and corrected.\n\nBooking ID: {{bookingId}}\nTransaction reference: {{transactionRef}}\nAdviser: {{adviserName}}\nWhen: {{when}}\nMeeting type: {{meetingType}}\nProvider event ID: {{providerEventId}}\nCorrection: {{correctionReason}}\n\nNo booking lifecycle event was created for this correction.", "Email", "text/plain", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Calendar event corrected - manager", "AFH Booking: Calendar Event Corrected", "calendar-event-corrected-manager", "v1", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "A manual Outlook calendar change was detected but could not be corrected automatically.\n\nBooking ID: {{bookingId}}\nTransaction reference: {{transactionRef}}\nAdviser: {{adviserName}}\nWhen: {{when}}\nMeeting type: {{meetingType}}\nProvider event ID: {{providerEventId}}\nReason: {{correctionReason}}\n\nOperations should review this booking and reconcile the Outlook event.", "Email", "text/plain", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Calendar event correction failed - adviser", "AFH Booking: Calendar Reconciliation Required", "calendar-event-correction-failed-adviser", "v1", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000010"), "A manual Outlook calendar change was detected but could not be corrected automatically.\n\nBooking ID: {{bookingId}}\nTransaction reference: {{transactionRef}}\nAdviser: {{adviserName}}\nWhen: {{when}}\nMeeting type: {{meetingType}}\nProvider event ID: {{providerEventId}}\nReason: {{correctionReason}}\n\nOperations should review this booking and reconcile the Outlook event.", "Email", "text/plain", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Calendar event correction failed - manager", "AFH Booking: Calendar Reconciliation Required", "calendar-event-correction-failed-manager", "v1", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("10000000-0000-0000-0000-000000000011"), "A manual Outlook calendar change was detected but could not be corrected automatically.\n\nBooking ID: {{bookingId}}\nTransaction reference: {{transactionRef}}\nAdviser: {{adviserName}}\nWhen: {{when}}\nMeeting type: {{meetingType}}\nProvider event ID: {{providerEventId}}\nReason: {{correctionReason}}\n\nOperations should review this booking and reconcile the Outlook event.", "Email", "text/plain", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Calendar event correction failed - contact centre", "AFH Booking: Calendar Reconciliation Required", "calendar-event-correction-failed-contact-centre", "v1", "System", new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var id in new[]
            {
                "10000000-0000-0000-0000-000000000005",
                "10000000-0000-0000-0000-000000000006",
                "10000000-0000-0000-0000-000000000007",
                "10000000-0000-0000-0000-000000000008",
                "10000000-0000-0000-0000-000000000009",
                "10000000-0000-0000-0000-000000000010",
                "10000000-0000-0000-0000-000000000011"
            })
            {
                migrationBuilder.DeleteData(
                    schema: "dbo",
                    table: "NotificationTemplates",
                    keyColumn: "Id",
                    keyValue: new Guid(id));
            }
        }
    }
}
