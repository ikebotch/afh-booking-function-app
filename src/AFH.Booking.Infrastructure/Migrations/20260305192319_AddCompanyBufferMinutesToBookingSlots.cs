using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBufferMinutesToBookingSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyBufferMinutes",
                table: "BookingSlots",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyBufferMinutes",
                table: "BookingSlots");
        }
    }
}
