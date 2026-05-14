using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFH.Booking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAdviserProfileProjectionModelKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdviserProfileProjections",
                columns: table => new
                {
                    AdviserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MailboxUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
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
                name: "ApplicationLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ContextId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalHistory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApprovalRequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequesterId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReasonDetail = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RequestedPayloadJson = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ApproverTargetType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApproverTargetValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApproverTargetDisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ExecutedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionError = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingTransactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProposedStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsRemote = table.Column<bool>(type: "bit", nullable: false),
                    MeetingType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LocationRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTransactions", x => x.Id);
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
                name: "ErrorRecordEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorRecordEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOperationAudits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FunctionName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QueryString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOperationAudits", x => x.Id);
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
                name: "LifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReasonNotes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RelatedBookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifecycleEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDispatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TransactionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LifecycleEventId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SmsRequested = table.Column<bool>(type: "bit", nullable: false),
                    EmailRequested = table.Column<bool>(type: "bit", nullable: false),
                    SmsStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmailStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OutcomeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FailureDetails = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "OperationalIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DetectedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BookingId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TransactionRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AdviserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProviderEventId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EscalationCount = table.Column<int>(type: "int", nullable: false),
                    LastEscalatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingSlots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdviserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdviserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    ScoreBreakdownJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TravelMinutes = table.Column<int>(type: "int", nullable: true),
                    CompanyBufferMinutes = table.Column<int>(type: "int", nullable: true),
                    DistanceMiles = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    TravelStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TravelMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LocationRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingSlots_BookingTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "BookingTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LifecycleSteps",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LifecycleEventId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifecycleSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LifecycleSteps_LifecycleEvents_LifecycleEventId",
                        column: x => x.LifecycleEventId,
                        principalTable: "LifecycleEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingHolds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlotId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    HoldExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CalendarProviderEventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingHolds_BookingSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "BookingSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserProfileProjections_IsActive_Region",
                table: "AdviserProfileProjections",
                columns: new[] { "IsActive", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_AdviserProfileProjections_LastSyncedUtc",
                table: "AdviserProfileProjections",
                column: "LastSyncedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_Category_OccurredUtc",
                table: "ApplicationLogs",
                columns: new[] { "Category", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_CorrelationId",
                table: "ApplicationLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_OccurredUtc",
                table: "ApplicationLogs",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationLogs_Operation_OccurredUtc",
                table: "ApplicationLogs",
                columns: new[] { "Operation", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalHistory_ApprovalRequestId",
                table: "ApprovalHistory",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalHistory_OccurredUtc",
                table: "ApprovalHistory",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_BookingId",
                table: "ApprovalRequests",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Status_RequestedUtc",
                table: "ApprovalRequests",
                columns: new[] { "Status", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_TransactionId",
                table: "ApprovalRequests",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_HoldExpiresUtc",
                table: "BookingHolds",
                column: "HoldExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_SlotId",
                table: "BookingHolds",
                column: "SlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_SlotId_Status",
                table: "BookingHolds",
                columns: new[] { "SlotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_Status",
                table: "BookingHolds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_AdviserId",
                table: "BookingSlots",
                column: "AdviserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_TransactionId_StartUtc",
                table: "BookingSlots",
                columns: new[] { "TransactionId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingTransactions_CreatedUtc",
                table: "BookingTransactions",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTransactions_TransactionRef",
                table: "BookingTransactions",
                column: "TransactionRef");

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
                name: "IX_ErrorRecordEntity_Code",
                table: "ErrorRecordEntity",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecordEntity_OccurredUtc",
                table: "ErrorRecordEntity",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSyncStates_UpdatedUtc",
                table: "IntegrationSyncStates",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_BookingId",
                table: "LifecycleEvents",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_CorrelationId",
                table: "LifecycleEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_EventType",
                table: "LifecycleEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_OccurredUtc",
                table: "LifecycleEvents",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleEvents_TransactionId",
                table: "LifecycleEvents",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleSteps_LifecycleEventId",
                table: "LifecycleSteps",
                column: "LifecycleEventId");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleSteps_LifecycleEventId_Sequence",
                table: "LifecycleSteps",
                columns: new[] { "LifecycleEventId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_City_Postcode",
                table: "Locations",
                columns: new[] { "City", "Postcode" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_DisplayName",
                table: "Locations",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_BookingId",
                table: "NotificationDispatches",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_CreatedUtc",
                table: "NotificationDispatches",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_LifecycleEventId",
                table: "NotificationDispatches",
                column: "LifecycleEventId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_ProviderMessageId",
                table: "NotificationDispatches",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDispatches_TransactionId",
                table: "NotificationDispatches",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIssues_AdviserId_Code_DetectedUtc",
                table: "OperationalIssues",
                columns: new[] { "AdviserId", "Code", "DetectedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIssues_ProviderEventId_Code_DetectedUtc",
                table: "OperationalIssues",
                columns: new[] { "ProviderEventId", "Code", "DetectedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviserProfileProjections");

            migrationBuilder.DropTable(
                name: "ApplicationLogs");

            migrationBuilder.DropTable(
                name: "ApprovalHistory");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "BookingHolds");

            migrationBuilder.DropTable(
                name: "DownstreamUpdates");

            migrationBuilder.DropTable(
                name: "DuplicateClientCases");

            migrationBuilder.DropTable(
                name: "EmailBounceEvents");

            migrationBuilder.DropTable(
                name: "ErrorRecordEntity");

            migrationBuilder.DropTable(
                name: "IntegrationOperationAudits");

            migrationBuilder.DropTable(
                name: "IntegrationSyncStates");

            migrationBuilder.DropTable(
                name: "LifecycleSteps");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "NotificationDispatches");

            migrationBuilder.DropTable(
                name: "OperationalIssues");

            migrationBuilder.DropTable(
                name: "BookingSlots");

            migrationBuilder.DropTable(
                name: "LifecycleEvents");

            migrationBuilder.DropTable(
                name: "BookingTransactions");
        }
    }
}
