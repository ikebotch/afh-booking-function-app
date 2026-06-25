using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations;

public partial class AddBookingAndRequestReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSequence<long>(
            name: "BookingReferenceNumber",
            schema: "dbo",
            startValue: 1001L);

        migrationBuilder.CreateSequence<long>(
            name: "ApprovalRequestReferenceNumber",
            schema: "dbo",
            startValue: 199L);

        migrationBuilder.AddColumn<string>(
            name: "Reference",
            table: "BookingHolds",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Reference",
            table: "ApprovalRequests",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BookingReference",
            table: "ApprovalRequests",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.Sql(
            """
            ;WITH OrderedBookings AS
            (
                SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedUtc, Id) + 1000 AS ReferenceNumber
                FROM dbo.BookingHolds
                WHERE Reference IS NULL
            )
            UPDATE h
            SET Reference = CONCAT('BK-', FORMAT(o.ReferenceNumber, '0000'), '-', UPPER(LEFT(REPLACE(h.Id, '-', ''), 4)))
            FROM dbo.BookingHolds h
            INNER JOIN OrderedBookings o ON o.Id = h.Id;
            """);

        migrationBuilder.Sql(
            """
            ;WITH OrderedRequests AS
            (
                SELECT Id, ROW_NUMBER() OVER (ORDER BY RequestedUtc, Id) + 198 AS ReferenceNumber
                FROM dbo.ApprovalRequests
                WHERE Reference IS NULL
            )
            UPDATE r
            SET
                Reference = CONCAT('REQ-', FORMAT(o.ReferenceNumber, '0000')),
                BookingReference = h.Reference
            FROM dbo.ApprovalRequests r
            INNER JOIN OrderedRequests o ON o.Id = r.Id
            LEFT JOIN dbo.BookingHolds h ON h.Id = r.BookingId;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_BookingHolds_Reference",
            table: "BookingHolds",
            column: "Reference",
            unique: true,
            filter: "[Reference] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_Reference",
            table: "ApprovalRequests",
            column: "Reference",
            unique: true,
            filter: "[Reference] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_BookingHolds_Reference",
            table: "BookingHolds");

        migrationBuilder.DropIndex(
            name: "IX_ApprovalRequests_Reference",
            table: "ApprovalRequests");

        migrationBuilder.DropColumn(
            name: "Reference",
            table: "BookingHolds");

        migrationBuilder.DropColumn(
            name: "Reference",
            table: "ApprovalRequests");

        migrationBuilder.DropColumn(
            name: "BookingReference",
            table: "ApprovalRequests");

        migrationBuilder.DropSequence(
            name: "BookingReferenceNumber",
            schema: "dbo");

        migrationBuilder.DropSequence(
            name: "ApprovalRequestReferenceNumber",
            schema: "dbo");
    }
}
