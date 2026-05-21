using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelSnapshotCoordinatesToBookingSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DestinationLatitude",
                table: "BookingSlots",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DestinationLongitude",
                table: "BookingSlots",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SourceLatitude",
                table: "BookingSlots",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SourceLongitude",
                table: "BookingSlots",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationLatitude",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "DestinationLongitude",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "SourceLatitude",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "SourceLongitude",
                table: "BookingSlots");
        }
    }
}
