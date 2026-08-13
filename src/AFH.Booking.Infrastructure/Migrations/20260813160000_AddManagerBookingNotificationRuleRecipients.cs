using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260813160000_AddManagerBookingNotificationRuleRecipients")]
    public partial class AddManagerBookingNotificationRuleRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO [dbo].[BookingNotificationRuleRecipients]
                    ([Id], [RuleId], [RecipientType], [Enabled], [CreatedUtc], [UpdatedUtc])
                SELECT
                    seed.[Id],
                    rule.[Id],
                    N'Manager',
                    seed.[Enabled],
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM (VALUES
                    (N'BookingConfirmed', CAST('8F7C2B57-2D84-4D52-B9D0-00F1C9CCDE01' AS uniqueidentifier), CAST(1 AS bit)),
                    (N'BookingRescheduled', CAST('10F4906D-1D29-41DE-8BAA-AECF96C5DE02' AS uniqueidentifier), CAST(1 AS bit)),
                    (N'BookingCancelled', CAST('9224F2F1-8408-423E-A318-CB3DD69FDE03' AS uniqueidentifier), CAST(1 AS bit)),
                    (N'BookingHoldCreated', CAST('D39F9F03-DF6F-4A1E-B7CB-DC94B2DDDE04' AS uniqueidentifier), CAST(0 AS bit))
                ) AS seed([NotificationType], [Id], [Enabled])
                INNER JOIN [dbo].[BookingNotificationRules] AS rule
                    ON rule.[SourceApplication] = N'Booking'
                   AND rule.[NotificationType] = seed.[NotificationType]
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [dbo].[BookingNotificationRuleRecipients] AS existing
                    WHERE existing.[RuleId] = rule.[Id]
                      AND existing.[RecipientType] = N'Manager'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [dbo].[BookingNotificationRuleRecipients]
                WHERE [Id] IN
                (
                    CAST('8F7C2B57-2D84-4D52-B9D0-00F1C9CCDE01' AS uniqueidentifier),
                    CAST('10F4906D-1D29-41DE-8BAA-AECF96C5DE02' AS uniqueidentifier),
                    CAST('9224F2F1-8408-423E-A318-CB3DD69FDE03' AS uniqueidentifier),
                    CAST('D39F9F03-DF6F-4A1E-B7CB-DC94B2DDDE04' AS uniqueidentifier)
                );
                """);
        }
    }
}
