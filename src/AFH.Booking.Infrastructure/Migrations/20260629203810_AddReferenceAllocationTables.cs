using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceAllocationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalRequestReferenceAllocations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR dbo.ApprovalRequestReferenceNumber"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequestReferenceAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingReferenceAllocations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR dbo.BookingReferenceNumber"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingReferenceAllocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestReferenceAllocations_CreatedUtc",
                table: "ApprovalRequestReferenceAllocations",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookingReferenceAllocations_CreatedUtc",
                table: "BookingReferenceAllocations",
                column: "CreatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRequestReferenceAllocations");

            migrationBuilder.DropTable(
                name: "BookingReferenceAllocations");
        }
    }
}
