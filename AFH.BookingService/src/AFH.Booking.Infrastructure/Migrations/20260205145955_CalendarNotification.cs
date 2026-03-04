using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CalendarNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarNotificationReceipts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientState = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarNotificationReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEventSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceiptId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CalendarId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: true),
                    FetchedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FetchError = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ChangeKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ICalUId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventSnapshots_CalendarNotificationReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "CalendarNotificationReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventSnapshots_ReceiptId",
                table: "CalendarEventSnapshots",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventSnapshots_UserId_ICalUId",
                table: "CalendarEventSnapshots",
                columns: new[] { "UserId", "ICalUId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventSnapshots_UserId_ProviderEventId_FetchedUtc",
                table: "CalendarEventSnapshots",
                columns: new[] { "UserId", "ProviderEventId", "FetchedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarNotificationReceipts_Accepted_ReceivedUtc",
                table: "CalendarNotificationReceipts",
                columns: new[] { "Accepted", "ReceivedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarNotificationReceipts_SubscriptionId_EventId_ReceivedUtc",
                table: "CalendarNotificationReceipts",
                columns: new[] { "SubscriptionId", "EventId", "ReceivedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventSnapshots");

            migrationBuilder.DropTable(
                name: "CalendarNotificationReceipts");
        }
    }
}
