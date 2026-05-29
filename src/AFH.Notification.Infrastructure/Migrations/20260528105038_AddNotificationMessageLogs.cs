using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationMessageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DispatchUid",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE [dbo].[NotificationDispatches] SET [DispatchUid] = NEWID() WHERE [DispatchUid] IS NULL");

            migrationBuilder.AlterColumn<Guid>(
                name: "DispatchUid",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_NotificationDispatches_DispatchUid",
                schema: "dbo",
                table: "NotificationDispatches",
                column: "DispatchUid");

            migrationBuilder.CreateTable(
                name: "NotificationMessageLogs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationDispatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationOutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceApplication = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotificationType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RecipientType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    RecipientMobile = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RenderDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationMessageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationMessageLogs_NotificationDispatches_NotificationDispatchId",
                        column: x => x.NotificationDispatchId,
                        principalSchema: "dbo",
                        principalTable: "NotificationDispatches",
                        principalColumn: "DispatchUid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationMessageLogs_NotificationOutbox_NotificationOutboxId",
                        column: x => x.NotificationOutboxId,
                        principalSchema: "dbo",
                        principalTable: "NotificationOutbox",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NotificationMessageLogs_NotificationTemplates_TemplateContentId",
                        column: x => x.TemplateContentId,
                        principalSchema: "dbo",
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_DispatchUid",
                schema: "dbo",
                table: "NotificationDispatches",
                column: "DispatchUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessageLogs_NotificationDispatchId",
                schema: "dbo",
                table: "NotificationMessageLogs",
                column: "NotificationDispatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessageLogs_NotificationOutboxId",
                schema: "dbo",
                table: "NotificationMessageLogs",
                column: "NotificationOutboxId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessageLogs_NotificationType_CreatedUtc",
                schema: "dbo",
                table: "NotificationMessageLogs",
                columns: new[] { "NotificationType", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessageLogs_RecipientEmail_CreatedUtc",
                schema: "dbo",
                table: "NotificationMessageLogs",
                columns: new[] { "RecipientEmail", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessageLogs_TemplateContentId",
                schema: "dbo",
                table: "NotificationMessageLogs",
                column: "TemplateContentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationMessageLogs",
                schema: "dbo");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_NotificationDispatches_DispatchUid",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_DispatchUid",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "DispatchUid",
                schema: "dbo",
                table: "NotificationDispatches");
        }
    }
}