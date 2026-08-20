using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyBookingNotificationDispatchTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[NotificationMessageLogs]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[NotificationMessageLogs];

                IF OBJECT_ID(N'[dbo].[EmailBounceEvents]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[EmailBounceEvents];

                IF OBJECT_ID(N'[dbo].[NotificationDispatches]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[NotificationDispatches];

                IF OBJECT_ID(N'[dbo].[NotificationOutbox]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[NotificationOutbox];

                IF OBJECT_ID(N'[dbo].[NotificationTemplates]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[NotificationTemplates];

                IF OBJECT_ID(N'[dbo].[NotificationSettings]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[NotificationSettings];

                IF OBJECT_ID(N'[dbo].[BookingNotificationRuleRecipients]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[BookingNotificationRuleRecipients];

                IF OBJECT_ID(N'[dbo].[BookingNotificationRuleChannels]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[BookingNotificationRuleChannels];

                IF OBJECT_ID(N'[dbo].[BookingNotificationRules]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[BookingNotificationRules];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: notification runtime and policy data now belongs to NotificationDb.
            // Roll forward by ensuring NotificationDb migrations are applied rather than recreating legacy BookingDb tables.
        }
    }
}
