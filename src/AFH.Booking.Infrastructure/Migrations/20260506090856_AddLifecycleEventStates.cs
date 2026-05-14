using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleEventStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewState",
                table: "LifecycleEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousState",
                table: "LifecycleEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_NewState",
                table: "LifecycleEvents",
                column: "NewState");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LifecycleEvents_NewState",
                table: "LifecycleEvents");

            migrationBuilder.DropColumn(
                name: "NewState",
                table: "LifecycleEvents");

            migrationBuilder.DropColumn(
                name: "PreviousState",
                table: "LifecycleEvents");
        }
    }
}
