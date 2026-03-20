using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitBookingTransactionSlotHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingTransactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProposedStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsRemote = table.Column<bool>(type: "bit", nullable: false),
                    MeetingType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LocationRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingSlots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdviserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdviserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    ScoreBreakdownJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TravelMinutes = table.Column<int>(type: "int", nullable: true),
                    DistanceMiles = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    TravelStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TravelMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LocationRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingSlots_BookingTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "BookingTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingHolds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SlotId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    HoldExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CalendarProviderEventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingHolds_BookingSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "BookingSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_HoldExpiresUtc",
                table: "BookingHolds",
                column: "HoldExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_SlotId",
                table: "BookingHolds",
                column: "SlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_Status",
                table: "BookingHolds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_AdviserId",
                table: "BookingSlots",
                column: "AdviserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_TransactionId_StartUtc",
                table: "BookingSlots",
                columns: new[] { "TransactionId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingTransactions_CreatedUtc",
                table: "BookingTransactions",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTransactions_TransactionRef",
                table: "BookingTransactions",
                column: "TransactionRef");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_City_Postcode",
                table: "Locations",
                columns: new[] { "City", "Postcode" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_DisplayName",
                table: "Locations",
                column: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingHolds");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "BookingSlots");

            migrationBuilder.DropTable(
                name: "BookingTransactions");
        }
    }
}
