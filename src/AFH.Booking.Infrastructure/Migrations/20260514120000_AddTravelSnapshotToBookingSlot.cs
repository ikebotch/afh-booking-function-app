using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelSnapshotToBookingSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationLocationRef",
                table: "BookingSlots",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationPostcode",
                table: "BookingSlots",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLocationRef",
                table: "BookingSlots",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePostcode",
                table: "BookingSlots",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TravelCalculatedUtc",
                table: "BookingSlots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TravelConfidence",
                table: "BookingSlots",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TravelDistanceMiles",
                table: "BookingSlots",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TravelProvider",
                table: "BookingSlots",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationLocationRef",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "DestinationPostcode",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "SourceLocationRef",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "SourcePostcode",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "TravelCalculatedUtc",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "TravelConfidence",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "TravelDistanceMiles",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "TravelProvider",
                table: "BookingSlots");
        }
    }
}
