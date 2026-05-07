using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    public partial class AddBookingAccessLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingAccessLinks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalBookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentBookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TransactionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAccessLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingAccessLinks_CurrentBookingId",
                table: "BookingAccessLinks",
                column: "CurrentBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAccessLinks_CurrentBookingId_RevokedUtc_ExpiresUtc",
                table: "BookingAccessLinks",
                columns: new[] { "CurrentBookingId", "RevokedUtc", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingAccessLinks_OriginalBookingId",
                table: "BookingAccessLinks",
                column: "OriginalBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAccessLinks_TokenHash",
                table: "BookingAccessLinks",
                column: "TokenHash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingAccessLinks");
        }
    }
}
