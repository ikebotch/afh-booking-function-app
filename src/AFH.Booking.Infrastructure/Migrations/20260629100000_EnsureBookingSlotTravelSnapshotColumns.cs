using AFH.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    [DbContext(typeof(BookingDbContext))]
    [Migration("20260629100000_EnsureBookingSlotTravelSnapshotColumns")]
    partial class EnsureBookingSlotTravelSnapshotColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationLocationRef') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [DestinationLocationRef] nvarchar(128) NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationPostcode') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [DestinationPostcode] nvarchar(32) NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourceLocationRef') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [SourceLocationRef] nvarchar(128) NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourcePostcode') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [SourcePostcode] nvarchar(32) NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelCalculatedUtc') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [TravelCalculatedUtc] datetime2 NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelConfidence') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [TravelConfidence] nvarchar(32) NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelDistanceMiles') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [TravelDistanceMiles] float NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelProvider') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [TravelProvider] nvarchar(64) NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationLatitude') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [DestinationLatitude] float NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationLongitude') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [DestinationLongitude] float NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourceLatitude') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [SourceLatitude] float NULL;

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourceLongitude') IS NULL
                    ALTER TABLE [dbo].[BookingSlots] ADD [SourceLongitude] float NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourceLongitude') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [SourceLongitude];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourceLatitude') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [SourceLatitude];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationLongitude') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [DestinationLongitude];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationLatitude') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [DestinationLatitude];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelProvider') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [TravelProvider];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelDistanceMiles') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [TravelDistanceMiles];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelConfidence') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [TravelConfidence];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'TravelCalculatedUtc') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [TravelCalculatedUtc];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourcePostcode') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [SourcePostcode];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'SourceLocationRef') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [SourceLocationRef];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationPostcode') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [DestinationPostcode];

                IF OBJECT_ID(N'[dbo].[BookingSlots]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BookingSlots', N'DestinationLocationRef') IS NOT NULL
                    ALTER TABLE [dbo].[BookingSlots] DROP COLUMN [DestinationLocationRef];
                """);
        }
    }
}
