using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyBookingNotificationPolicyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[BookingNotificationRules]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[BookingNotificationRules]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [SourceApplication] nvarchar(100) NOT NULL,
                        [NotificationType] nvarchar(150) NOT NULL,
                        [Enabled] bit NOT NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        [UpdatedUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_BookingNotificationRules] PRIMARY KEY ([Id])
                    );

                    CREATE UNIQUE INDEX [IX_BookingNotificationRules_SourceApplication_NotificationType]
                        ON [dbo].[BookingNotificationRules] ([SourceApplication], [NotificationType]);
                END

                IF OBJECT_ID(N'[dbo].[BookingNotificationRuleChannels]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[BookingNotificationRuleChannels]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [RuleId] uniqueidentifier NOT NULL,
                        [Channel] nvarchar(50) NOT NULL,
                        [Enabled] bit NOT NULL,
                        [TemplateKey] nvarchar(150) NOT NULL,
                        [TemplateVersion] nvarchar(50) NOT NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        [UpdatedUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_BookingNotificationRuleChannels] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_BookingNotificationRuleChannels_BookingNotificationRules_RuleId]
                            FOREIGN KEY ([RuleId]) REFERENCES [dbo].[BookingNotificationRules] ([Id]) ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX [IX_BookingNotificationRuleChannels_RuleId_Channel]
                        ON [dbo].[BookingNotificationRuleChannels] ([RuleId], [Channel]);
                END

                IF OBJECT_ID(N'[dbo].[BookingNotificationRuleRecipients]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[BookingNotificationRuleRecipients]
                    (
                        [Id] uniqueidentifier NOT NULL,
                        [RuleId] uniqueidentifier NOT NULL,
                        [RecipientType] nvarchar(100) NOT NULL,
                        [Enabled] bit NOT NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        [UpdatedUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_BookingNotificationRuleRecipients] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_BookingNotificationRuleRecipients_BookingNotificationRules_RuleId]
                            FOREIGN KEY ([RuleId]) REFERENCES [dbo].[BookingNotificationRules] ([Id]) ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX [IX_BookingNotificationRuleRecipients_RuleId_RecipientType]
                        ON [dbo].[BookingNotificationRuleRecipients] ([RuleId], [RecipientType]);
                END
                """);
        }
    }
}
