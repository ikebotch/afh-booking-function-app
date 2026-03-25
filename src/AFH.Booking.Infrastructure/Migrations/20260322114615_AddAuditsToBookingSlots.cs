using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditsToBookingSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdviserAvailabilityBlocks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdviserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CalendarId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    ChangeKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ICalUId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceReceiptId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserAvailabilityBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdviserProfileProjections",
                columns: table => new
                {
                    AdviserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HomePostcode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    SkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageRadiusMiles = table.Column<double>(type: "float", nullable: true),
                    MaxTravelTimeMinutes = table.Column<int>(type: "int", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviserProfileProjections", x => x.AdviserId);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReasonDetail = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Reviewer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownstreamUpdates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownstreamUpdates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DuplicateClientCases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PrimaryTransactionRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DuplicateTransactionRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RaisedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RaisedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResolvedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateClientCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailBounceEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReasonDetail = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailBounceEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOperationAudit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FunctionName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QueryString = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOperationAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationSyncStates",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationSyncStates", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDispatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SmsRequested = table.Column<bool>(type: "bit", nullable: false),
                    EmailRequested = table.Column<bool>(type: "bit", nullable: false),
                    SmsStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmailStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MessageBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDispatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserAvailabilityBlocks_AdviserId_LastSyncedUtc",
                table: "AdviserAvailabilityBlocks",
                columns: new[] { "AdviserId", "LastSyncedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserAvailabilityBlocks_AdviserId_ProviderEventId",
                table: "AdviserAvailabilityBlocks",
                columns: new[] { "AdviserId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdviserAvailabilityBlocks_AdviserId_StartUtc_EndUtc",
                table: "AdviserAvailabilityBlocks",
                columns: new[] { "AdviserId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserProfileProjections_IsActive_Region",
                table: "AdviserProfileProjections",
                columns: new[] { "IsActive", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserProfileProjections_LastSyncedUtc",
                table: "AdviserProfileProjections",
                column: "LastSyncedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_BookingId",
                table: "ApprovalRequests",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Status_RequestedUtc",
                table: "ApprovalRequests",
                columns: new[] { "Status", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DownstreamUpdates_BookingId",
                table: "DownstreamUpdates",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_DownstreamUpdates_Status_CreatedUtc",
                table: "DownstreamUpdates",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateClientCases_Status_RaisedUtc",
                table: "DuplicateClientCases",
                columns: new[] { "Status", "RaisedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailBounceEvents_ProviderMessageId",
                table: "EmailBounceEvents",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailBounceEvents_ReceivedUtc",
                table: "EmailBounceEvents",
                column: "ReceivedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailBounceEvents_RecipientEmail",
                table: "EmailBounceEvents",
                column: "RecipientEmail");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOperationAudit_CorrelationId",
                table: "IntegrationOperationAudit",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOperationAudit_CreatedUtc",
                table: "IntegrationOperationAudit",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOperationAudit_FunctionName_CreatedUtc",
                table: "IntegrationOperationAudit",
                columns: new[] { "FunctionName", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSyncStates_UpdatedUtc",
                table: "IntegrationSyncStates",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_BookingId",
                table: "NotificationDispatches",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_CreatedUtc",
                table: "NotificationDispatches",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_ProviderMessageId",
                table: "NotificationDispatches",
                column: "ProviderMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviserAvailabilityBlocks");

            migrationBuilder.DropTable(
                name: "AdviserProfileProjections");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "DownstreamUpdates");

            migrationBuilder.DropTable(
                name: "DuplicateClientCases");

            migrationBuilder.DropTable(
                name: "EmailBounceEvents");

            migrationBuilder.DropTable(
                name: "IntegrationOperationAudit");

            migrationBuilder.DropTable(
                name: "IntegrationSyncStates");

            migrationBuilder.DropTable(
                name: "NotificationDispatches");
        }
    }
}
