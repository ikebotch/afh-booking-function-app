using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260820120000_AddApprovalRequestNotificationPolicies")]
    public partial class AddApprovalRequestNotificationPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();

                IF NOT EXISTS (
                    SELECT 1 FROM [dbo].[BookingNotificationRules]
                    WHERE [SourceApplication] = N'Booking' AND [NotificationType] = N'AdviserRequestSubmitted')
                BEGIN
                    INSERT INTO [dbo].[BookingNotificationRules] ([Id], [SourceApplication], [NotificationType], [Enabled], [CreatedUtc], [UpdatedUtc])
                    VALUES (CAST('19C2F130-6649-42ED-82CB-95F0D8706D01' AS uniqueidentifier), N'Booking', N'AdviserRequestSubmitted', 1, @now, @now);
                END;

                IF NOT EXISTS (
                    SELECT 1 FROM [dbo].[BookingNotificationRules]
                    WHERE [SourceApplication] = N'Booking' AND [NotificationType] = N'AdviserRequestOutcome')
                BEGIN
                    INSERT INTO [dbo].[BookingNotificationRules] ([Id], [SourceApplication], [NotificationType], [Enabled], [CreatedUtc], [UpdatedUtc])
                    VALUES (CAST('9B9BE0BE-7633-4576-BA6D-24EF060A0E6D' AS uniqueidentifier), N'Booking', N'AdviserRequestOutcome', 1, @now, @now);
                END;

                INSERT INTO [dbo].[BookingNotificationRuleChannels] ([Id], [RuleId], [Channel], [Enabled], [TemplateKey], [TemplateVersion], [CreatedUtc], [UpdatedUtc])
                SELECT seed.[Id], rule.[Id], seed.[Channel], seed.[Enabled], seed.[TemplateKey], N'v1', @now, @now
                FROM (VALUES
                    (N'AdviserRequestSubmitted', CAST('14179CDB-D3A0-4142-80A0-387403FD9201' AS uniqueidentifier), N'Email', CAST(1 AS bit), N'adviser-request-submitted'),
                    (N'AdviserRequestSubmitted', CAST('6AF1C6AD-C7C2-4D98-8859-C36F9F4D9202' AS uniqueidentifier), N'Sms', CAST(0 AS bit), N'adviser-request-submitted-sms'),
                    (N'AdviserRequestOutcome', CAST('9BEEC961-A8F0-4CC9-BEC7-D5C28C3357AF' AS uniqueidentifier), N'Email', CAST(1 AS bit), N'adviser-request-outcome'),
                    (N'AdviserRequestOutcome', CAST('9BEEC961-A8F0-4CC9-BEC7-D5C28C3333BF' AS uniqueidentifier), N'Sms', CAST(0 AS bit), N'adviser-request-outcome-sms')
                ) AS seed([NotificationType], [Id], [Channel], [Enabled], [TemplateKey])
                INNER JOIN [dbo].[BookingNotificationRules] AS rule
                    ON rule.[SourceApplication] = N'Booking'
                   AND rule.[NotificationType] = seed.[NotificationType]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [dbo].[BookingNotificationRuleChannels] AS existing
                    WHERE existing.[RuleId] = rule.[Id]
                      AND existing.[Channel] = seed.[Channel]);

                INSERT INTO [dbo].[BookingNotificationRuleRecipients] ([Id], [RuleId], [RecipientType], [Enabled], [CreatedUtc], [UpdatedUtc])
                SELECT seed.[Id], rule.[Id], seed.[RecipientType], 1, @now, @now
                FROM (VALUES
                    (N'AdviserRequestSubmitted', CAST('7809FF7D-0721-430F-9B62-0FFB57DC9401' AS uniqueidentifier), N'Client'),
                    (N'AdviserRequestSubmitted', CAST('20AAF403-B3D1-4247-B6D6-885EAF8B9402' AS uniqueidentifier), N'Adviser'),
                    (N'AdviserRequestSubmitted', CAST('F8495119-62A9-45EE-9BF0-AE099ADA9403' AS uniqueidentifier), N'Manager'),
                    (N'AdviserRequestSubmitted', CAST('F1E20620-C6B2-4871-B79F-EF9D23509404' AS uniqueidentifier), N'ContactCentre'),
                    (N'AdviserRequestOutcome', CAST('E5CD76C9-DC76-4A92-9686-F152B3D4C901' AS uniqueidentifier), N'Client'),
                    (N'AdviserRequestOutcome', CAST('9F053BD8-906F-4F48-8D22-CAC5590AFCCB' AS uniqueidentifier), N'Adviser'),
                    (N'AdviserRequestOutcome', CAST('358AC452-B5AD-4373-A593-1FF936E83298' AS uniqueidentifier), N'Manager'),
                    (N'AdviserRequestOutcome', CAST('8B951DB9-B91E-443F-9715-55DF020B0B4B' AS uniqueidentifier), N'ContactCentre')
                ) AS seed([NotificationType], [Id], [RecipientType])
                INNER JOIN [dbo].[BookingNotificationRules] AS rule
                    ON rule.[SourceApplication] = N'Booking'
                   AND rule.[NotificationType] = seed.[NotificationType]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [dbo].[BookingNotificationRuleRecipients] AS existing
                    WHERE existing.[RuleId] = rule.[Id]
                      AND existing.[RecipientType] = seed.[RecipientType]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [dbo].[BookingNotificationRuleRecipients]
                WHERE [Id] IN
                (
                    CAST('7809FF7D-0721-430F-9B62-0FFB57DC9401' AS uniqueidentifier),
                    CAST('20AAF403-B3D1-4247-B6D6-885EAF8B9402' AS uniqueidentifier),
                    CAST('F8495119-62A9-45EE-9BF0-AE099ADA9403' AS uniqueidentifier),
                    CAST('F1E20620-C6B2-4871-B79F-EF9D23509404' AS uniqueidentifier),
                    CAST('E5CD76C9-DC76-4A92-9686-F152B3D4C901' AS uniqueidentifier),
                    CAST('9F053BD8-906F-4F48-8D22-CAC5590AFCCB' AS uniqueidentifier),
                    CAST('358AC452-B5AD-4373-A593-1FF936E83298' AS uniqueidentifier),
                    CAST('8B951DB9-B91E-443F-9715-55DF020B0B4B' AS uniqueidentifier)
                );

                DELETE FROM [dbo].[BookingNotificationRuleChannels]
                WHERE [Id] IN
                (
                    CAST('14179CDB-D3A0-4142-80A0-387403FD9201' AS uniqueidentifier),
                    CAST('6AF1C6AD-C7C2-4D98-8859-C36F9F4D9202' AS uniqueidentifier),
                    CAST('9BEEC961-A8F0-4CC9-BEC7-D5C28C3357AF' AS uniqueidentifier),
                    CAST('9BEEC961-A8F0-4CC9-BEC7-D5C28C3333BF' AS uniqueidentifier)
                );

                DELETE FROM [dbo].[BookingNotificationRules]
                WHERE [Id] IN
                (
                    CAST('19C2F130-6649-42ED-82CB-95F0D8706D01' AS uniqueidentifier),
                    CAST('9B9BE0BE-7633-4576-BA6D-24EF060A0E6D' AS uniqueidentifier)
                );
                """);
        }
    }
}
