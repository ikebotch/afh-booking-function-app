using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCalendarNotificationReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarNotificationReceipts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientState = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarNotificationReceipts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarNotificationReceipts");
        }
    }
}
