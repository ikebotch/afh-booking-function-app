using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class BookingOperationalStoreInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BookingOperationalStoreInitializer> _logger;

    public BookingOperationalStoreInitializer(
        IServiceProvider services,
        ILogger<BookingOperationalStoreInitializer> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.ApprovalRequests', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ApprovalRequests (
                        Id nvarchar(64) NOT NULL PRIMARY KEY,
                        BookingId nvarchar(64) NOT NULL,
                        ChangeType nvarchar(32) NOT NULL,
                        RequestedBy nvarchar(32) NOT NULL,
                        Status nvarchar(32) NOT NULL,
                        RequestedUtc datetime2 NOT NULL,
                        ReasonCode nvarchar(128) NULL,
                        ReasonDetail nvarchar(1024) NULL,
                        Reviewer nvarchar(128) NULL,
                        ReviewedUtc datetime2 NULL,
                        ReviewNotes nvarchar(1024) NULL
                    );
                    CREATE INDEX IX_ApprovalRequests_Status_RequestedUtc ON dbo.ApprovalRequests(Status, RequestedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.NotificationDispatches', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.NotificationDispatches (
                        Id nvarchar(64) NOT NULL PRIMARY KEY,
                        BookingId nvarchar(64) NOT NULL,
                        EventType nvarchar(64) NOT NULL,
                        SmsRequested bit NOT NULL,
                        EmailRequested bit NOT NULL,
                        SmsStatus nvarchar(32) NOT NULL,
                        EmailStatus nvarchar(32) NOT NULL,
                        RecipientPhone nvarchar(64) NULL,
                        RecipientEmail nvarchar(256) NULL,
                        ProviderMessageId nvarchar(128) NULL,
                        MessageBody nvarchar(4000) NULL,
                        CreatedUtc datetime2 NOT NULL,
                        UpdatedUtc datetime2 NULL
                    );
                    CREATE INDEX IX_NotificationDispatches_CreatedUtc ON dbo.NotificationDispatches(CreatedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.EmailBounceEvents', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.EmailBounceEvents (
                        Id nvarchar(64) NOT NULL PRIMARY KEY,
                        ProviderMessageId nvarchar(128) NULL,
                        RecipientEmail nvarchar(256) NULL,
                        ReasonCode nvarchar(128) NULL,
                        ReasonDetail nvarchar(2048) NULL,
                        OccurredUtc datetime2 NOT NULL,
                        ReceivedUtc datetime2 NOT NULL
                    );
                    CREATE INDEX IX_EmailBounceEvents_ReceivedUtc ON dbo.EmailBounceEvents(ReceivedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.DuplicateClientCases', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.DuplicateClientCases (
                        Id nvarchar(64) NOT NULL PRIMARY KEY,
                        PrimaryTransactionRef nvarchar(256) NOT NULL,
                        DuplicateTransactionRef nvarchar(256) NOT NULL,
                        Status nvarchar(32) NOT NULL,
                        Notes nvarchar(2048) NULL,
                        RaisedBy nvarchar(128) NULL,
                        RaisedUtc datetime2 NOT NULL,
                        Resolution nvarchar(512) NULL,
                        ResolvedBy nvarchar(128) NULL,
                        ResolvedUtc datetime2 NULL
                    );
                    CREATE INDEX IX_DuplicateClientCases_Status_RaisedUtc ON dbo.DuplicateClientCases(Status, RaisedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.DownstreamUpdates', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.DownstreamUpdates (
                        Id nvarchar(64) NOT NULL PRIMARY KEY,
                        BookingId nvarchar(64) NOT NULL,
                        ChangeType nvarchar(64) NOT NULL,
                        TransactionRef nvarchar(256) NOT NULL,
                        PayloadJson nvarchar(max) NOT NULL,
                        Status nvarchar(32) NOT NULL,
                        AttemptCount int NOT NULL,
                        ErrorMessage nvarchar(2048) NULL,
                        CreatedUtc datetime2 NOT NULL,
                        ProcessedUtc datetime2 NULL
                    );
                    CREATE INDEX IX_DownstreamUpdates_Status_CreatedUtc ON dbo.DownstreamUpdates(Status, CreatedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.AdviserAvailabilityBlocks', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.AdviserAvailabilityBlocks (
                        Id nvarchar(64) NOT NULL PRIMARY KEY,
                        AdviserId nvarchar(256) NOT NULL,
                        ProviderEventId nvarchar(512) NOT NULL,
                        CalendarId nvarchar(512) NULL,
                        Subject nvarchar(512) NULL,
                        StartUtc datetime2 NOT NULL,
                        EndUtc datetime2 NOT NULL,
                        IsCancelled bit NOT NULL,
                        ChangeKey nvarchar(256) NULL,
                        ICalUId nvarchar(512) NULL,
                        LastSyncedUtc datetime2 NOT NULL,
                        SourceReceiptId nvarchar(64) NULL
                    );
                    CREATE UNIQUE INDEX UX_AdviserAvailabilityBlocks_AdviserId_ProviderEventId
                        ON dbo.AdviserAvailabilityBlocks(AdviserId, ProviderEventId);
                    CREATE INDEX IX_AdviserAvailabilityBlocks_AdviserId_StartUtc_EndUtc
                        ON dbo.AdviserAvailabilityBlocks(AdviserId, StartUtc, EndUtc);
                    CREATE INDEX IX_AdviserAvailabilityBlocks_AdviserId_LastSyncedUtc
                        ON dbo.AdviserAvailabilityBlocks(AdviserId, LastSyncedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.AdviserProfileProjections', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.AdviserProfileProjections (
                        AdviserId nvarchar(256) NOT NULL PRIMARY KEY,
                        DisplayName nvarchar(256) NOT NULL,
                        Region nvarchar(128) NULL,
                        HomePostcode nvarchar(32) NULL,
                        IsActive bit NOT NULL,
                        Rating float NOT NULL,
                        SkillsJson nvarchar(max) NOT NULL,
                        CoverageRadiusMiles float NULL,
                        MaxTravelTimeMinutes int NULL,
                        LastSyncedUtc datetime2 NOT NULL,
                        SourceVersion nvarchar(128) NULL
                    );
                    CREATE INDEX IX_AdviserProfileProjections_IsActive_Region
                        ON dbo.AdviserProfileProjections(IsActive, Region);
                    CREATE INDEX IX_AdviserProfileProjections_LastSyncedUtc
                        ON dbo.AdviserProfileProjections(LastSyncedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.IntegrationSyncStates', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.IntegrationSyncStates (
                        [Key] nvarchar(128) NOT NULL PRIMARY KEY,
                        [Value] nvarchar(max) NOT NULL,
                        UpdatedUtc datetime2 NOT NULL
                    );
                    CREATE INDEX IX_IntegrationSyncStates_UpdatedUtc
                        ON dbo.IntegrationSyncStates(UpdatedUtc);
                END
                """,
                cancellationToken: cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID('dbo.IntegrationOperationAudit', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.IntegrationOperationAudit (
                        Id nvarchar(64) NOT NULL PRIMARY KEY,
                        ServiceName nvarchar(64) NOT NULL,
                        FunctionName nvarchar(256) NOT NULL,
                        Method nvarchar(16) NOT NULL,
                        Path nvarchar(512) NOT NULL,
                        QueryString nvarchar(2048) NULL,
                        CorrelationId nvarchar(128) NULL,
                        OperationId nvarchar(128) NOT NULL,
                        StatusCode int NOT NULL,
                        DurationMs bigint NOT NULL,
                        ErrorType nvarchar(128) NULL,
                        ErrorMessage nvarchar(2048) NULL,
                        CreatedUtc datetime2 NOT NULL
                    );
                    CREATE INDEX IX_IntegrationOperationAudit_CreatedUtc
                        ON dbo.IntegrationOperationAudit(CreatedUtc);
                    CREATE INDEX IX_IntegrationOperationAudit_CorrelationId
                        ON dbo.IntegrationOperationAudit(CorrelationId);
                    CREATE INDEX IX_IntegrationOperationAudit_FunctionName_CreatedUtc
                        ON dbo.IntegrationOperationAudit(FunctionName, CreatedUtc);
                END
                """,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operational store initialization failed. New workflow features may not persist correctly.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
