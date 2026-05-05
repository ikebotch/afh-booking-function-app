using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    public partial class AddLifecycleEventTriggerReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerReason",
                table: "LifecycleEvents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_TriggerReason",
                table: "LifecycleEvents",
                column: "TriggerReason");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LifecycleEvents_TriggerReason",
                table: "LifecycleEvents");

            migrationBuilder.DropColumn(
                name: "TriggerReason",
                table: "LifecycleEvents");
        }
    }
}
