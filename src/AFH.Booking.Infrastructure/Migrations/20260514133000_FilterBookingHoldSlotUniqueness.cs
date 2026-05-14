using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    public partial class FilterBookingHoldSlotUniqueness : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingHolds_SlotId",
                table: "BookingHolds");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_SlotId",
                table: "BookingHolds",
                column: "SlotId",
                unique: true,
                filter: "[Status] IN (0, 1)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingHolds_SlotId",
                table: "BookingHolds");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_SlotId",
                table: "BookingHolds",
                column: "SlotId",
                unique: true);
        }
    }
}
