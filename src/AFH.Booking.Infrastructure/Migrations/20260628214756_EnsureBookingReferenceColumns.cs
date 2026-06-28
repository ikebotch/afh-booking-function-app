using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureBookingReferenceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF SCHEMA_ID(N'dbo') IS NULL
                    EXEC(N'CREATE SCHEMA [dbo]');

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.sequences
                    WHERE [name] = N'ApprovalRequestReferenceNumber'
                      AND SCHEMA_NAME([schema_id]) = N'dbo'
                )
                    CREATE SEQUENCE [dbo].[ApprovalRequestReferenceNumber] AS bigint
                        START WITH 199 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CACHE;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.sequences
                    WHERE [name] = N'BookingReferenceNumber'
                      AND SCHEMA_NAME([schema_id]) = N'dbo'
                )
                    CREATE SEQUENCE [dbo].[BookingReferenceNumber] AS bigint
                        START WITH 1001 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CACHE;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[BookingHolds]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingHolds', N'Reference') IS NULL
                    ALTER TABLE [dbo].[BookingHolds]
                        ADD [Reference] nvarchar(32) NULL;

                IF OBJECT_ID(N'[dbo].[ApprovalRequests]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.ApprovalRequests', N'Reference') IS NULL
                    ALTER TABLE [dbo].[ApprovalRequests]
                        ADD [Reference] nvarchar(32) NULL;

                IF OBJECT_ID(N'[dbo].[ApprovalRequests]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.ApprovalRequests', N'BookingReference') IS NULL
                    ALTER TABLE [dbo].[ApprovalRequests]
                        ADD [BookingReference] nvarchar(32) NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'ProjectContext') IS NULL
                    ALTER TABLE [dbo].[BookingSlots]
                        ADD [ProjectContext] nvarchar(128) NOT NULL
                            CONSTRAINT [DF_BookingSlots_ProjectContext] DEFAULT N'Booking';
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[FeatureFlags]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[FeatureFlags]
                    (
                        [Key] nvarchar(150) NOT NULL,
                        [Name] nvarchar(200) NOT NULL,
                        [Description] nvarchar(500) NULL,
                        [IsEnabled] bit NOT NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        [UpdatedUtc] datetime2 NOT NULL,
                        [UpdatedBy] nvarchar(150) NULL,
                        CONSTRAINT [PK_FeatureFlags] PRIMARY KEY ([Key])
                    );
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[BookingHolds]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingHolds', N'Reference') IS NOT NULL
                BEGIN
                    ;WITH OrderedBookings AS
                    (
                        SELECT [Id], ROW_NUMBER() OVER (ORDER BY [CreatedUtc], [Id]) + 1000 AS [ReferenceNumber]
                        FROM [dbo].[BookingHolds]
                        WHERE [Reference] IS NULL
                    )
                    UPDATE h
                    SET [Reference] = CONCAT(
                        'BK-',
                        FORMAT(o.[ReferenceNumber], '0000'),
                        '-',
                        UPPER(LEFT(REPLACE(CONVERT(nvarchar(64), h.[Id]), '-', ''), 4)))
                    FROM [dbo].[BookingHolds] h
                    INNER JOIN OrderedBookings o ON o.[Id] = h.[Id]
                    WHERE h.[Reference] IS NULL;
                END;

                IF OBJECT_ID(N'[dbo].[ApprovalRequests]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.ApprovalRequests', N'Reference') IS NOT NULL
                   AND COL_LENGTH(N'dbo.ApprovalRequests', N'BookingReference') IS NOT NULL
                BEGIN
                    ;WITH OrderedRequests AS
                    (
                        SELECT [Id], ROW_NUMBER() OVER (ORDER BY [RequestedUtc], [Id]) + 198 AS [ReferenceNumber]
                        FROM [dbo].[ApprovalRequests]
                        WHERE [Reference] IS NULL
                    )
                    UPDATE r
                    SET
                        [Reference] = CONCAT('REQ-', FORMAT(o.[ReferenceNumber], '0000')),
                        [BookingReference] = h.[Reference]
                    FROM [dbo].[ApprovalRequests] r
                    INNER JOIN OrderedRequests o ON o.[Id] = r.[Id]
                    LEFT JOIN [dbo].[BookingHolds] h ON h.[Id] = r.[BookingId]
                    WHERE r.[Reference] IS NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[BookingHolds]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingHolds', N'Reference') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE [name] = N'IX_BookingHolds_Reference'
                         AND [object_id] = OBJECT_ID(N'[dbo].[BookingHolds]')
                   )
                    CREATE UNIQUE INDEX [IX_BookingHolds_Reference]
                        ON [dbo].[BookingHolds] ([Reference])
                        WHERE [Reference] IS NOT NULL;

                IF OBJECT_ID(N'[dbo].[ApprovalRequests]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.ApprovalRequests', N'Reference') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.indexes
                       WHERE [name] = N'IX_ApprovalRequests_Reference'
                         AND [object_id] = OBJECT_ID(N'[dbo].[ApprovalRequests]')
                   )
                    CREATE UNIQUE INDEX [IX_ApprovalRequests_Reference]
                        ON [dbo].[ApprovalRequests] ([Reference])
                        WHERE [Reference] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op: this migration repairs databases whose migration
            // history drifted from the schema expected by the booking model.
        }
    }
}
