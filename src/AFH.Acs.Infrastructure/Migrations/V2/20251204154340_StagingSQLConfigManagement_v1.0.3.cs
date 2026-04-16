using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Acs.Recorder.Infrastructure.Migrations.V2
{
    /// <inheritdoc />
    public partial class StagingSQLConfigManagement_v103 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "RECORDING_END_UTC",
                table: "MEETING_RECORDINGS",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateTable(
                name: "APPLICATION_LOGS",
                columns: table => new
                {
                    LOG_ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TIMESTAMP_UTC = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FUNCTION_NAME = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LOG_LEVEL = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MESSAGE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EXCEPTION_MESSAGE = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EXCEPTION_STACK = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CORRELATION_ID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    REQUEST_ID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EVENT_TYPE = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PAYLOAD_JSON = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPLICATION_LOGS", x => x.LOG_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATION_LOGS_CORRELATION_ID",
                table: "APPLICATION_LOGS",
                column: "CORRELATION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATION_LOGS_TIMESTAMP_UTC",
                table: "APPLICATION_LOGS",
                column: "TIMESTAMP_UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "APPLICATION_LOGS");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RECORDING_END_UTC",
                table: "MEETING_RECORDINGS",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
