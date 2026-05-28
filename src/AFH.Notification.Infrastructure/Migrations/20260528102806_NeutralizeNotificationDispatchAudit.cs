using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NeutralizeNotificationDispatchAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_OutcomeCode_CreatedUtc",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.AlterColumn<string>(
                name: "SmsStatus",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<bool>(
                name: "SmsRequested",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "OutcomeCode",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "EmailStatus",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<bool>(
                name: "EmailRequested",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "BookingId",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "RecipientMobile",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReferenceId",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReferenceType",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_SourceApplication_SourceReferenceType_SourceReferenceId",
                schema: "dbo",
                table: "NotificationDispatches",
                columns: new[] { "SourceApplication", "SourceReferenceType", "SourceReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_Status_CreatedUtc",
                schema: "dbo",
                table: "NotificationDispatches",
                columns: new[] { "Status", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_SourceApplication_SourceReferenceType_SourceReferenceId",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDispatches_Status_CreatedUtc",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "RecipientMobile",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "SourceReferenceId",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "SourceReferenceType",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "dbo",
                table: "NotificationDispatches");

            migrationBuilder.AlterColumn<string>(
                name: "SmsStatus",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "SmsRequested",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OutcomeCode",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailStatus",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "EmailRequested",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BookingId",
                schema: "dbo",
                table: "NotificationDispatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_OutcomeCode_CreatedUtc",
                schema: "dbo",
                table: "NotificationDispatches",
                columns: new[] { "OutcomeCode", "CreatedUtc" });
        }
    }
}
