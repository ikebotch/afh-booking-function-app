using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingNotificationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "NotificationDispatches",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateVersion",
                table: "NotificationDispatches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingNotificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceApplication = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingNotificationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingNotificationRuleChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingNotificationRuleChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingNotificationRuleChannels_BookingNotificationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "BookingNotificationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingNotificationRuleRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingNotificationRuleRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingNotificationRuleRecipients_BookingNotificationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "BookingNotificationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BookingNotificationRules",
                columns: new[] { "Id", "CreatedUtc", "Enabled", "NotificationType", "SourceApplication", "UpdatedUtc" },
                values: new object[,]
                {
                    { new Guid("4a6f3c68-4eb7-4788-bd13-6908b33c7951"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "BookingConfirmed", "Booking", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4e428654-c797-4f91-97fa-bd7b5086395a"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, "BookingHoldCreated", "Booking", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7f6c8f21-9d36-481e-b5ad-08454d0036a6"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "BookingRescheduled", "Booking", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f6096d32-d3c7-49d4-8d16-9b7135e66274"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "BookingCancelled", "Booking", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "BookingNotificationRuleChannels",
                columns: new[] { "Id", "Channel", "CreatedUtc", "Enabled", "RuleId", "TemplateKey", "TemplateVersion", "UpdatedUtc" },
                values: new object[,]
                {
                    { new Guid("04eec961-a8f0-4cc9-bec7-d5c28c3356af"), "Email", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("4e428654-c797-4f91-97fa-bd7b5086395a"), "booking-hold", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6e093647-7ca9-4f98-9110-352533da95ef"), "Sms", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("4a6f3c68-4eb7-4788-bd13-6908b33c7951"), "booking-confirmed-sms", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("728736f1-89e2-4315-bab4-4670b526a201"), "Email", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("4a6f3c68-4eb7-4788-bd13-6908b33c7951"), "booking-confirmed", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("77fe97da-c2c5-44fb-8394-67e2f89d04b7"), "Email", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("7f6c8f21-9d36-481e-b5ad-08454d0036a6"), "booking-rescheduled", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9cb40ca9-95c0-4fd2-9d87-51e711164a7b"), "Email", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("f6096d32-d3c7-49d4-8d16-9b7135e66274"), "booking-cancelled", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("aa463227-5113-4c56-881e-07d47085c18e"), "Sms", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("4e428654-c797-4f91-97fa-bd7b5086395a"), "booking-hold-sms", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2b3e321-e4e0-4223-a840-b262bfe6ef41"), "Sms", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("f6096d32-d3c7-49d4-8d16-9b7135e66274"), "booking-cancelled-sms", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d5a7d12e-d0a9-40a3-8911-841cd5753c1e"), "Sms", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("7f6c8f21-9d36-481e-b5ad-08454d0036a6"), "booking-rescheduled-sms", "v1", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "BookingNotificationRuleRecipients",
                columns: new[] { "Id", "CreatedUtc", "Enabled", "RecipientType", "RuleId", "UpdatedUtc" },
                values: new object[,]
                {
                    { new Guid("12d8a812-96fc-4020-93cd-30a82efa3c02"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "Adviser", new Guid("4a6f3c68-4eb7-4788-bd13-6908b33c7951"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("27650cba-e5cf-4777-903d-24123832d1a3"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "ContactCentre", new Guid("f6096d32-d3c7-49d4-8d16-9b7135e66274"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("42cda070-c4cd-4fdb-ab79-c3d6870baf7e"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "Adviser", new Guid("7f6c8f21-9d36-481e-b5ad-08454d0036a6"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("55f92361-2f74-4128-a794-703eff8ca9e3"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "ContactCentre", new Guid("7f6c8f21-9d36-481e-b5ad-08454d0036a6"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("566301d9-a5d6-4e2d-a688-65b6d11c00f2"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "Adviser", new Guid("f6096d32-d3c7-49d4-8d16-9b7135e66274"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6bc808c6-53a3-473f-972d-d8871678e4d4"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, "Adviser", new Guid("4e428654-c797-4f91-97fa-bd7b5086395a"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("84186f63-a40d-419b-933d-573106880aeb"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, "ContactCentre", new Guid("4e428654-c797-4f91-97fa-bd7b5086395a"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("982657c1-2041-4fed-9cb1-5a802e258704"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "Client", new Guid("7f6c8f21-9d36-481e-b5ad-08454d0036a6"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9c987676-c344-476a-a57a-8a22e2d97922"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), false, "Client", new Guid("4e428654-c797-4f91-97fa-bd7b5086395a"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a689b522-d031-44db-b145-e8109350c271"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "Client", new Guid("f6096d32-d3c7-49d4-8d16-9b7135e66274"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f3096f1b-fd20-4cc3-a431-22f1d1021d01"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "Client", new Guid("4a6f3c68-4eb7-4788-bd13-6908b33c7951"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f7726ed6-0939-49d3-b18c-5f2e34959f03"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "ContactCentre", new Guid("4a6f3c68-4eb7-4788-bd13-6908b33c7951"), new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingNotificationRuleChannels_RuleId_Channel",
                table: "BookingNotificationRuleChannels",
                columns: new[] { "RuleId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingNotificationRuleRecipients_RuleId_RecipientType",
                table: "BookingNotificationRuleRecipients",
                columns: new[] { "RuleId", "RecipientType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingNotificationRules_SourceApplication_NotificationType",
                table: "BookingNotificationRules",
                columns: new[] { "SourceApplication", "NotificationType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingNotificationRuleChannels");

            migrationBuilder.DropTable(
                name: "BookingNotificationRuleRecipients");

            migrationBuilder.DropTable(
                name: "BookingNotificationRules");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "NotificationDispatches");
        }
    }
}
