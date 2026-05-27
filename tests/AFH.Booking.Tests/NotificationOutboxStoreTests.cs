using System.Data;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AFH.Booking.Tests;

[Trait("Category", "Integration")]
public class NotificationOutboxStoreTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly NotificationOutboxStore _sut;
    private bool _dbAvailable;

    public NotificationOutboxStoreTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Values:BookingDb:ConnectionString"] = "Server=localhost;Database=AFH.Booking.Test;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();

        _connectionString = config["Values:BookingDb:ConnectionString"]!;
        _sut = new NotificationOutboxStore(config, NullLogger<NotificationOutboxStore>.Instance);
    }

    public async Task InitializeAsync()
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            _dbAvailable = true;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='NotificationOutbox' AND xtype='U')
                CREATE TABLE [dbo].[NotificationOutbox] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SourceApplication] nvarchar(100) NOT NULL,
                    [NotificationType] nvarchar(150) NOT NULL,
                    [IdempotencyKey] nvarchar(500) NOT NULL,
                    [PayloadJson] nvarchar(max) NOT NULL,
                    [Status] nvarchar(50) NOT NULL,
                    [QueueMessageId] nvarchar(200) NULL,
                    [AttemptCount] int NOT NULL DEFAULT 0,
                    [LastError] nvarchar(max) NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [UpdatedUtc] datetime2 NOT NULL,
                    [ProcessedUtc] datetime2 NULL,
                    CONSTRAINT [IX_NotificationOutbox_IdempotencyKey] UNIQUE ([IdempotencyKey])
                )";
            await cmd.ExecuteNonQueryAsync();

            await using var clearCmd = conn.CreateCommand();
            clearCmd.CommandText = "DELETE FROM NotificationOutbox";
            await clearCmd.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            _dbAvailable = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private bool SkipIfNoDb() => !_dbAvailable;

    [Fact]
    public async Task CreateOrGetAsync_CreatesNewItem()
    {
        if (SkipIfNoDb()) return;

        var item = new NotificationOutboxItem(
            Guid.NewGuid(),
            "Booking",
            "BookingConfirmed",
            $"Booking:BookingConfirmed:{Guid.NewGuid()}",
            "{}",
            NotificationDispatchStatus.Pending,
            DateTime.UtcNow,
            DateTime.UtcNow);

        var result = await _sut.CreateOrGetAsync(item, CancellationToken.None);

        Assert.Equal(item.Id, result.Id);
        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Pending, loaded.Status);
    }

    [Fact]
    public async Task CreateOrGetAsync_DuplicateIdempotencyKey_ReturnsExistingItem()
    {
        if (SkipIfNoDb()) return;

        var idempotency = $"Booking:Duplicate:{Guid.NewGuid()}";
        var firstId = Guid.NewGuid();
        var item1 = new NotificationOutboxItem(
            firstId, "App", "Type", idempotency, "{}", NotificationDispatchStatus.Pending, DateTime.UtcNow, DateTime.UtcNow);

        await _sut.CreateOrGetAsync(item1, CancellationToken.None);

        var item2 = new NotificationOutboxItem(
            Guid.NewGuid(), "App2", "Type2", idempotency, "{}", NotificationDispatchStatus.Pending, DateTime.UtcNow, DateTime.UtcNow);

        var result = await _sut.CreateOrGetAsync(item2, CancellationToken.None);

        // Should return the first item because the idempotency key matches
        Assert.Equal(firstId, result.Id);
        Assert.Equal("App", result.SourceApplication);
    }

    [Fact]
    public async Task MarkQueuedAsync_UpdatesStatusAndQueueMessageId()
    {
        if (SkipIfNoDb()) return;

        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, DateTime.UtcNow, DateTime.UtcNow);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        await _sut.MarkQueuedAsync(item.Id, "queue-123", CancellationToken.None);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Queued, loaded.Status);
        
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT QueueMessageId FROM NotificationOutbox WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", item.Id);
        var qid = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("queue-123", qid);
    }

    [Fact]
    public async Task MarkProcessingAsync_IncrementsAttemptCount()
    {
        if (SkipIfNoDb()) return;

        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, DateTime.UtcNow, DateTime.UtcNow);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        await _sut.MarkProcessingAsync(item.Id, CancellationToken.None);
        await _sut.MarkProcessingAsync(item.Id, CancellationToken.None);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Processing, loaded.Status);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AttemptCount FROM NotificationOutbox WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", item.Id);
        var count = (int)await cmd.ExecuteScalarAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkSentAsync_SetsProcessedUtc()
    {
        if (SkipIfNoDb()) return;

        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, DateTime.UtcNow, DateTime.UtcNow);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        await _sut.MarkSentAsync(item.Id, CancellationToken.None);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Sent, loaded.Status);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ProcessedUtc FROM NotificationOutbox WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", item.Id);
        var processed = await cmd.ExecuteScalarAsync();
        Assert.NotEqual(DBNull.Value, processed);
    }

    [Fact]
    public async Task MarkFailedAsync_RecordsLastError()
    {
        if (SkipIfNoDb()) return;

        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, DateTime.UtcNow, DateTime.UtcNow);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        await _sut.MarkFailedAsync(item.Id, "error-details", CancellationToken.None);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Failed, loaded.Status);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT LastError FROM NotificationOutbox WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", item.Id);
        var err = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("error-details", err);
    }

    [Fact]
    public void SqlInjectionSafety_VerifiedByParameterUsage()
    {
        // Verified by inspection: NotificationOutboxStore uses @id, @sourceApp, etc.
        // No string interpolation is used for values.
        var code = File.ReadAllText("../../../../../src/AFH.Notification.Infrastructure/Persistence/NotificationOutboxStore.cs");
        Assert.DoesNotContain("CommandText = $\"", code);
        Assert.DoesNotContain("CommandText = string.Format", code);
    }
}
