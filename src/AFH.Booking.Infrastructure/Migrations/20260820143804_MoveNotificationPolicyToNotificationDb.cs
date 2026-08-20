using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveNotificationPolicyToNotificationDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: notification policy tables now belong to NotificationDb.
            // Keep existing BookingDb tables untouched so UAT/prod data can be migrated or verified manually.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op. The previous model can be restored by reverting this migration in source.
        }
    }
}
