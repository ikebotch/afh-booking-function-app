using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Acs.Recorder.Infrastructure.Migrations.V2
{
    /// <inheritdoc />
    public partial class StagingSQLConfigManagement_v102 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADVISERS",
                columns: table => new
                {
                    ADVISER_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FULL_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EMAIL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    REGION = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LEAD_TECH_FLAG = table.Column<bool>(type: "bit", nullable: false),
                    ACTIVE_FLAG = table.Column<bool>(type: "bit", nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UPDATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADVISERS", x => x.ADVISER_ID);
                });

            migrationBuilder.CreateTable(
                name: "ATR_TEMPLATES",
                columns: table => new
                {
                    ATR_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RISK_LEVEL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PARAGRAPH_TEXT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KEYPOINT_HEADER = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ATR_TEMPLATES", x => x.ATR_ID);
                });

            migrationBuilder.CreateTable(
                name: "CHECKLIST_TEMPLATES",
                columns: table => new
                {
                    TEMPLATE_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MEETING_TYPE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ACTIVE_FLAG = table.Column<bool>(type: "bit", nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHECKLIST_TEMPLATES", x => x.TEMPLATE_ID);
                });

            migrationBuilder.CreateTable(
                name: "LEADS",
                columns: table => new
                {
                    LEAD_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CLIENT_ID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CLIENT_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CLIENT_EMAIL = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SOURCE_SYSTEM = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STATUS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UPDATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LEADS", x => x.LEAD_ID);
                });

            migrationBuilder.CreateTable(
                name: "CHECKLIST_ITEM_TEMPLATES",
                columns: table => new
                {
                    TEMPLATE_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ITEM_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DISPLAY_TEXT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DISPLAY_ORDER = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHECKLIST_ITEM_TEMPLATES", x => new { x.TEMPLATE_ID, x.ITEM_ID });
                    table.ForeignKey(
                        name: "FK_CHECKLIST_ITEM_TEMPLATES_CHECKLIST_TEMPLATES_TEMPLATE_ID",
                        column: x => x.TEMPLATE_ID,
                        principalTable: "CHECKLIST_TEMPLATES",
                        principalColumn: "TEMPLATE_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEETINGS",
                columns: table => new
                {
                    MEETING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LEAD_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ADVISER_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GROUP_ID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GRAPH_EVENT_ID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MEETING_TYPE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TITLE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    START_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    END_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CLIENT_EMAIL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CONSENT_TO_RECORDING = table.Column<bool>(type: "bit", nullable: false),
                    CONSENT_TIMESTAMP_UTC = table.Column<DateTime>(type: "datetime2", nullable: true),
                    STATUS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UPDATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETINGS", x => x.MEETING_ID);
                    table.ForeignKey(
                        name: "FK_MEETINGS_ADVISERS_ADVISER_ID",
                        column: x => x.ADVISER_ID,
                        principalTable: "ADVISERS",
                        principalColumn: "ADVISER_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MEETINGS_LEADS_LEAD_ID",
                        column: x => x.LEAD_ID,
                        principalTable: "LEADS",
                        principalColumn: "LEAD_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEETING_ATR_ANALYSES",
                columns: table => new
                {
                    MEETING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CLIENT_ATR_TEXT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MATCHED_TEMPLATE_IDS_JSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MISSING_KEYPOINTS_JSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NOTES = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETING_ATR_ANALYSES", x => x.MEETING_ID);
                    table.ForeignKey(
                        name: "FK_MEETING_ATR_ANALYSES_MEETINGS_MEETING_ID",
                        column: x => x.MEETING_ID,
                        principalTable: "MEETINGS",
                        principalColumn: "MEETING_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEETING_ATTENDEES",
                columns: table => new
                {
                    MEETING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EMAIL = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ROLE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RESPONSE_STATUS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RESPONSE_TIME_UTC = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETING_ATTENDEES", x => new { x.MEETING_ID, x.EMAIL });
                    table.ForeignKey(
                        name: "FK_MEETING_ATTENDEES_MEETINGS_MEETING_ID",
                        column: x => x.MEETING_ID,
                        principalTable: "MEETINGS",
                        principalColumn: "MEETING_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEETING_CHECKLIST_ITEMS",
                columns: table => new
                {
                    MEETING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ITEM_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DISPLAY_TEXT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IS_COMPLETED = table.Column<bool>(type: "bit", nullable: false),
                    COMPLETED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETING_CHECKLIST_ITEMS", x => new { x.MEETING_ID, x.ITEM_ID });
                    table.ForeignKey(
                        name: "FK_MEETING_CHECKLIST_ITEMS_MEETINGS_MEETING_ID",
                        column: x => x.MEETING_ID,
                        principalTable: "MEETINGS",
                        principalColumn: "MEETING_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEETING_NOTES",
                columns: table => new
                {
                    NOTE_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MEETING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ADVISER_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NOTE_TEXT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UPDATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETING_NOTES", x => x.NOTE_ID);
                    table.ForeignKey(
                        name: "FK_MEETING_NOTES_ADVISERS_ADVISER_ID",
                        column: x => x.ADVISER_ID,
                        principalTable: "ADVISERS",
                        principalColumn: "ADVISER_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MEETING_NOTES_MEETINGS_MEETING_ID",
                        column: x => x.MEETING_ID,
                        principalTable: "MEETINGS",
                        principalColumn: "MEETING_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MEETING_RECORDINGS",
                columns: table => new
                {
                    RECORDING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MEETING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GROUP_ID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BLOB_NAME = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BLOB_URL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RECORDING_START_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RECORDING_END_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DURATION_SECONDS = table.Column<int>(type: "int", nullable: true),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETING_RECORDINGS", x => x.RECORDING_ID);
                    table.ForeignKey(
                        name: "FK_MEETING_RECORDINGS_MEETINGS_MEETING_ID",
                        column: x => x.MEETING_ID,
                        principalTable: "MEETINGS",
                        principalColumn: "MEETING_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEETING_TRANSCRIPTIONS",
                columns: table => new
                {
                    TRANSCRIPTION_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MEETING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RECORDING_ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LANGUAGE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RAW_JSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FULL_TEXT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SUMMARY_TEXT = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CREATED_AT_UTC = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETING_TRANSCRIPTIONS", x => x.TRANSCRIPTION_ID);
                    table.ForeignKey(
                        name: "FK_MEETING_TRANSCRIPTIONS_MEETINGS_MEETING_ID",
                        column: x => x.MEETING_ID,
                        principalTable: "MEETINGS",
                        principalColumn: "MEETING_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MEETING_TRANSCRIPTIONS_MEETING_RECORDINGS_RECORDING_ID",
                        column: x => x.RECORDING_ID,
                        principalTable: "MEETING_RECORDINGS",
                        principalColumn: "RECORDING_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MEETING_NOTES_ADVISER_ID",
                table: "MEETING_NOTES",
                column: "ADVISER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEETING_NOTES_MEETING_ID",
                table: "MEETING_NOTES",
                column: "MEETING_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEETING_RECORDINGS_MEETING_ID",
                table: "MEETING_RECORDINGS",
                column: "MEETING_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEETING_TRANSCRIPTIONS_MEETING_ID",
                table: "MEETING_TRANSCRIPTIONS",
                column: "MEETING_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MEETING_TRANSCRIPTIONS_RECORDING_ID",
                table: "MEETING_TRANSCRIPTIONS",
                column: "RECORDING_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEETINGS_ADVISER_ID",
                table: "MEETINGS",
                column: "ADVISER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEETINGS_LEAD_ID",
                table: "MEETINGS",
                column: "LEAD_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ATR_TEMPLATES");

            migrationBuilder.DropTable(
                name: "CHECKLIST_ITEM_TEMPLATES");

            migrationBuilder.DropTable(
                name: "MEETING_ATR_ANALYSES");

            migrationBuilder.DropTable(
                name: "MEETING_ATTENDEES");

            migrationBuilder.DropTable(
                name: "MEETING_CHECKLIST_ITEMS");

            migrationBuilder.DropTable(
                name: "MEETING_NOTES");

            migrationBuilder.DropTable(
                name: "MEETING_TRANSCRIPTIONS");

            migrationBuilder.DropTable(
                name: "CHECKLIST_TEMPLATES");

            migrationBuilder.DropTable(
                name: "MEETING_RECORDINGS");

            migrationBuilder.DropTable(
                name: "MEETINGS");

            migrationBuilder.DropTable(
                name: "ADVISERS");

            migrationBuilder.DropTable(
                name: "LEADS");
        }
    }
}
