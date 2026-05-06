using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    public partial class AddMeetingTopics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingTopics",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTopics", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTopics_IsActive_SortOrder",
                table: "MeetingTopics",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.Sql("""
                INSERT INTO MeetingTopics (Code, Label, IsDefault, IsActive, SortOrder, CreatedUtc)
                VALUES
                    ('Retirement', 'Retirement', 0, 1, 10, SYSUTCDATETIME()),
                    ('Pension', 'Pension', 0, 1, 20, SYSUTCDATETIME()),
                    ('Will', 'Will', 0, 1, 30, SYSUTCDATETIME());
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingTopics");
        }
    }
}
