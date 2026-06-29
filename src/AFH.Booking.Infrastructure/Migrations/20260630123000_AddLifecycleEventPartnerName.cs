using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260630123000_AddLifecycleEventPartnerName")]
    public partial class AddLifecycleEventPartnerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PartnerName",
                table: "LifecycleEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_PartnerName",
                table: "LifecycleEvents",
                column: "PartnerName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LifecycleEvents_PartnerName",
                table: "LifecycleEvents");

            migrationBuilder.DropColumn(
                name: "PartnerName",
                table: "LifecycleEvents");
        }
    }
}
