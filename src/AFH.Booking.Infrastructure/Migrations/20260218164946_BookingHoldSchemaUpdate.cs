using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookingHoldSchemaUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedUtc",
                table: "BookingHolds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "BookingHolds",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "BookingHolds",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_SlotId_Status",
                table: "BookingHolds",
                columns: new[] { "SlotId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingHolds_SlotId_Status",
                table: "BookingHolds");

            migrationBuilder.DropColumn(
                name: "ReleasedUtc",
                table: "BookingHolds");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BookingHolds");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "BookingHolds");
        }
    }
}
