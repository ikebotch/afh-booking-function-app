using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCalendarSubscriptionRowVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.CalendarSubscriptions', 'U') IS NOT NULL
BEGIN
    -- Drop RowVersion if it exists AND is not the proper rowversion/timestamp type
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.CalendarSubscriptions')
          AND c.name = 'RowVersion'
          AND t.name <> 'timestamp' -- SQL Server reports rowversion as 'timestamp' in sys.types
    )
    BEGIN
        ALTER TABLE dbo.CalendarSubscriptions DROP COLUMN RowVersion;
    END

    -- Add RowVersion if missing
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID('dbo.CalendarSubscriptions')
          AND c.name = 'RowVersion'
    )
    BEGIN
        ALTER TABLE dbo.CalendarSubscriptions
        ADD RowVersion rowversion NOT NULL;
    END
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.CalendarSubscriptions', 'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID('dbo.CalendarSubscriptions')
          AND c.name = 'RowVersion'
    )
    BEGIN
        ALTER TABLE dbo.CalendarSubscriptions DROP COLUMN RowVersion;
    END
END
");
        }
    }
}
