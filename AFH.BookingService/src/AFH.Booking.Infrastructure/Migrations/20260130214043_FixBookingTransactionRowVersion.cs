using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixBookingTransactionRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.BookingTransactions')
      AND c.name = 'RowVersion'
)
BEGIN
    ALTER TABLE dbo.BookingTransactions DROP COLUMN RowVersion;
END
");

            migrationBuilder.Sql(@"
ALTER TABLE dbo.BookingTransactions
ADD RowVersion rowversion NOT NULL;
");
        }



        /// <inheritdoc />

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE dbo.BookingTransactions DROP COLUMN RowVersion;
");
        }
    }
}
