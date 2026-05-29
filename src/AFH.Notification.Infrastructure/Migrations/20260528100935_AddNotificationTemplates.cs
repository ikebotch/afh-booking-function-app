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
            EnsureAuditTables(migrationBuilder);
            AddDispatchColumnIfMissing(migrationBuilder, "CompletedUtc", "datetime2 NULL");
            AddDispatchColumnIfMissing(migrationBuilder, "MessageSubject", "nvarchar(500) NULL");
            AddDispatchColumnIfMissing(migrationBuilder, "RecipientType", "nvarchar(100) NULL");
            AddDispatchColumnIfMissing(migrationBuilder, "TemplateKey", "nvarchar(150) NULL");
            AddDispatchColumnIfMissing(migrationBuilder, "TemplateVersion", "nvarchar(50) NULL");

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

            CreateDispatchIndexIfMissing(
                migrationBuilder,
                "IX_NotificationDispatches_NotificationType_CreatedUtc",
                "[NotificationType], [CreatedUtc]");

            CreateDispatchIndexIfMissing(
                migrationBuilder,
                "IX_NotificationDispatches_OutcomeCode_CreatedUtc",
                "[OutcomeCode], [CreatedUtc]");

            CreateDispatchIndexIfMissing(
                migrationBuilder,
                "IX_NotificationDispatches_RecipientEmail_CreatedUtc",
                "[RecipientEmail], [CreatedUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TemplateKey_TemplateVersion_Channel",
                schema: "dbo",
                table: "NotificationTemplates",
                columns: new[] { "TemplateKey", "TemplateVersion", "Channel" },
                unique: true);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_NotificationDispatches_NotificationOutbox_NotificationOutboxId' AND [parent_object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    ALTER TABLE [dbo].[NotificationDispatches] ADD CONSTRAINT [FK_NotificationDispatches_NotificationOutbox_NotificationOutboxId] FOREIGN KEY ([NotificationOutboxId]) REFERENCES [dbo].[NotificationOutbox] ([Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDispatches_NotificationOutbox_NotificationOutboxId",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_NotificationType_CreatedUtc",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_OutcomeCode_CreatedUtc",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_RecipientEmail_CreatedUtc",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropTable(
                name: "NotificationTemplates",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CompletedUtc",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "MessageSubject",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "RecipientType",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                schema: "dbo",
                table: "NotificationDispatches");
        }

        private static void EnsureAuditTables(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[NotificationDispatches]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[NotificationDispatches] (
                        [Id] nvarchar(64) NOT NULL,
                        CONSTRAINT [PK_NotificationDispatches] PRIMARY KEY ([Id])
                    );
                END

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'BookingId') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [BookingId] nvarchar(64) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'TransactionId') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [TransactionId] nvarchar(64) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'TransactionRef') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [TransactionRef] nvarchar(128) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'LifecycleEventId') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [LifecycleEventId] nvarchar(64) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'CorrelationId') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [CorrelationId] nvarchar(150) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'EventType') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [EventType] nvarchar(64) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'SmsRequested') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [SmsRequested] bit NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'EmailRequested') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [EmailRequested] bit NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'SmsStatus') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [SmsStatus] nvarchar(32) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'EmailStatus') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [EmailStatus] nvarchar(32) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'OutcomeCode') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [OutcomeCode] nvarchar(64) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'FailureDetails') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [FailureDetails] nvarchar(max) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'RecipientPhone') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [RecipientPhone] nvarchar(64) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'RecipientEmail') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [RecipientEmail] nvarchar(320) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'ProviderMessageId') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [ProviderMessageId] nvarchar(200) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'MessageBody') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [MessageBody] nvarchar(max) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'NotificationOutboxId') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [NotificationOutboxId] uniqueidentifier NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'SourceApplication') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [SourceApplication] nvarchar(100) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'NotificationType') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [NotificationType] nvarchar(150) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'Channel') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [Channel] nvarchar(50) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'ProviderName') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [ProviderName] nvarchar(100) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'TemplateName') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [TemplateName] nvarchar(200) NULL;

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'CreatedUtc') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [CreatedUtc] datetime2 NOT NULL CONSTRAINT [DF_NotificationDispatches_CreatedUtc] DEFAULT SYSUTCDATETIME();

                IF COL_LENGTH(N'dbo.NotificationDispatches', N'UpdatedUtc') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [UpdatedUtc] datetime2 NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationDispatches_BookingId' AND [object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    CREATE INDEX [IX_NotificationDispatches_BookingId] ON [dbo].[NotificationDispatches] ([BookingId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationDispatches_CreatedUtc' AND [object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    CREATE INDEX [IX_NotificationDispatches_CreatedUtc] ON [dbo].[NotificationDispatches] ([CreatedUtc]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationDispatches_LifecycleEventId' AND [object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    CREATE INDEX [IX_NotificationDispatches_LifecycleEventId] ON [dbo].[NotificationDispatches] ([LifecycleEventId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationDispatches_NotificationOutboxId' AND [object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    CREATE INDEX [IX_NotificationDispatches_NotificationOutboxId] ON [dbo].[NotificationDispatches] ([NotificationOutboxId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationDispatches_ProviderMessageId' AND [object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    CREATE INDEX [IX_NotificationDispatches_ProviderMessageId] ON [dbo].[NotificationDispatches] ([ProviderMessageId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationDispatches_TransactionId' AND [object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    CREATE INDEX [IX_NotificationDispatches_TransactionId] ON [dbo].[NotificationDispatches] ([TransactionId]);

                IF OBJECT_ID(N'[dbo].[EmailBounceEvents]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[EmailBounceEvents] (
                        [Id] nvarchar(64) NOT NULL,
                        [ProviderMessageId] nvarchar(200) NULL,
                        [RecipientEmail] nvarchar(320) NULL,
                        [ReasonCode] nvarchar(128) NULL,
                        [ReasonDetail] nvarchar(2048) NULL,
                        [OccurredUtc] datetime2 NOT NULL,
                        [ReceivedUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_EmailBounceEvents] PRIMARY KEY ([Id])
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EmailBounceEvents_ProviderMessageId' AND [object_id] = OBJECT_ID(N'[dbo].[EmailBounceEvents]'))
                    CREATE INDEX [IX_EmailBounceEvents_ProviderMessageId] ON [dbo].[EmailBounceEvents] ([ProviderMessageId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EmailBounceEvents_RecipientEmail' AND [object_id] = OBJECT_ID(N'[dbo].[EmailBounceEvents]'))
                    CREATE INDEX [IX_EmailBounceEvents_RecipientEmail] ON [dbo].[EmailBounceEvents] ([RecipientEmail]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EmailBounceEvents_ReceivedUtc' AND [object_id] = OBJECT_ID(N'[dbo].[EmailBounceEvents]'))
                    CREATE INDEX [IX_EmailBounceEvents_ReceivedUtc] ON [dbo].[EmailBounceEvents] ([ReceivedUtc]);
                """);
        }

        private static void AddDispatchColumnIfMissing(MigrationBuilder migrationBuilder, string name, string definition)
        {
            migrationBuilder.Sql($"""
                IF COL_LENGTH(N'dbo.NotificationDispatches', N'{name}') IS NULL
                    ALTER TABLE [dbo].[NotificationDispatches] ADD [{name}] {definition};
                """);
        }

        private static void CreateDispatchIndexIfMissing(MigrationBuilder migrationBuilder, string name, string columns)
        {
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'{name}' AND [object_id] = OBJECT_ID(N'[dbo].[NotificationDispatches]'))
                    CREATE INDEX [{name}] ON [dbo].[NotificationDispatches] ({columns});
                """);
        }
    }
}
