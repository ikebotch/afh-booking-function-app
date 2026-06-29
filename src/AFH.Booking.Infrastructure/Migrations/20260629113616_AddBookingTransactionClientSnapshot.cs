using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingTransactionClientSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingReference",
                table: "BookingTransactions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientAddressLine1",
                table: "BookingTransactions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientAddressLine2",
                table: "BookingTransactions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientCounty",
                table: "BookingTransactions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientEmail",
                table: "BookingTransactions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "BookingTransactions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientPostcode",
                table: "BookingTransactions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientTown",
                table: "BookingTransactions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE tx
                   SET [BookingReference] = latestHold.[Reference]
                  FROM [dbo].[BookingTransactions] tx
                  OUTER APPLY (
                        SELECT TOP (1) h.[Reference]
                          FROM [dbo].[BookingSlots] s
                          JOIN [dbo].[BookingHolds] h ON h.[SlotId] = s.[Id]
                         WHERE s.[TransactionId] = tx.[Id]
                           AND h.[Reference] IS NOT NULL
                         ORDER BY
                           CASE WHEN h.[Status] = 1 THEN 0 ELSE 1 END,
                           h.[CreatedUtc] DESC
                  ) latestHold
                 WHERE tx.[BookingReference] IS NULL
                   AND latestHold.[Reference] IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BookingTransactions_BookingReference",
                table: "BookingTransactions",
                column: "BookingReference",
                unique: true,
                filter: "[BookingReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingTransactions_BookingReference",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "BookingReference",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "ClientAddressLine1",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "ClientAddressLine2",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "ClientCounty",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "ClientEmail",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "ClientName",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "ClientPostcode",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "ClientTown",
                table: "BookingTransactions");
        }
    }
}
