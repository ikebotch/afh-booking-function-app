using System.Data;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationOutboxStore : INotificationOutboxStore
{
    private readonly string _connectionString;
    private readonly ILogger<NotificationOutboxStore> _logger;

    public NotificationOutboxStore(IConfiguration configuration, ILogger<NotificationOutboxStore> logger)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? configuration["Values:ConnectionStrings:BookingDb"]
            ?? configuration["Values:BookingDb:ConnectionString"]
            ?? throw new InvalidOperationException("BookingDb connection string is not configured.");
        _logger = logger;
    }

    public async Task<NotificationOutboxItem> CreateOrGetAsync(NotificationOutboxItem item, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        try
        {
            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO NotificationOutbox (
                    Id, SourceApplication, NotificationType, IdempotencyKey, 
                    PayloadJson, Status, AttemptCount, CreatedUtc, UpdatedUtc
                )
                VALUES (
                    @id, @sourceApp, @type, @idempotency,
                    @payload, @status, @attempt, @created, @updated
                )";

            insertCmd.Parameters.AddWithValue("@id", item.Id);
            insertCmd.Parameters.AddWithValue("@sourceApp", item.SourceApplication);
            insertCmd.Parameters.AddWithValue("@type", item.NotificationType);
            insertCmd.Parameters.AddWithValue("@idempotency", item.IdempotencyKey);
            insertCmd.Parameters.AddWithValue("@payload", item.PayloadJson);
            insertCmd.Parameters.AddWithValue("@status", item.Status.ToString());
            insertCmd.Parameters.AddWithValue("@attempt", 0);
            insertCmd.Parameters.AddWithValue("@created", item.CreatedUtc);
            insertCmd.Parameters.AddWithValue("@updated", item.UpdatedUtc);

            await insertCmd.ExecuteNonQueryAsync(ct);
            return item;
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) // Unique constraint violation
        {
            _logger.LogInformation("Duplicate IdempotencyKey '{IdempotencyKey}' detected. Returning existing outbox item.", item.IdempotencyKey);
            
            await using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = @"
                SELECT Id, SourceApplication, NotificationType, IdempotencyKey, PayloadJson, Status, CreatedUtc, UpdatedUtc 
                FROM NotificationOutbox 
                WHERE IdempotencyKey = @idempotency";
            selectCmd.Parameters.AddWithValue("@idempotency", item.IdempotencyKey);

            await using var reader = await selectCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new NotificationOutboxItem(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    Enum.Parse<NotificationDispatchStatus>(reader.GetString(5)),
                    reader.GetDateTime(6),
                    reader.GetDateTime(7)
                );
            }

            // Fallback in case of highly concurrent deletion (rare in outbox)
            throw new InvalidOperationException($"Failed to retrieve duplicate outbox item for key {item.IdempotencyKey}", ex);
        }
    }

    public async Task<NotificationOutboxItem?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"
            SELECT Id, SourceApplication, NotificationType, IdempotencyKey, PayloadJson, Status, CreatedUtc, UpdatedUtc 
            FROM NotificationOutbox 
            WHERE Id = @id";
        selectCmd.Parameters.AddWithValue("@id", id);

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new NotificationOutboxItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                Enum.Parse<NotificationDispatchStatus>(reader.GetString(5)),
                reader.GetDateTime(6),
                reader.GetDateTime(7)
            );
        }

        return null;
    }

    public async Task MarkQueuedAsync(Guid id, string queueMessageId, CancellationToken ct)
    {
        await UpdateInternalAsync(id, NotificationDispatchStatus.Queued, queueMessageId, null, ct);
    }

    public async Task MarkProcessingAsync(Guid id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = @"
            UPDATE NotificationOutbox 
            SET Status = @status, 
                AttemptCount = AttemptCount + 1,
                UpdatedUtc = @now 
            WHERE Id = @id";
        
        updateCmd.Parameters.AddWithValue("@id", id);
        updateCmd.Parameters.AddWithValue("@status", NotificationDispatchStatus.Processing.ToString());
        updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

        await updateCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = @"
            UPDATE NotificationOutbox 
            SET Status = @status, 
                ProcessedUtc = @now,
                UpdatedUtc = @now 
            WHERE Id = @id";
        
        updateCmd.Parameters.AddWithValue("@id", id);
        updateCmd.Parameters.AddWithValue("@status", NotificationDispatchStatus.Sent.ToString());
        updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

        await updateCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string lastError, CancellationToken ct)
    {
        await UpdateInternalAsync(id, NotificationDispatchStatus.Failed, null, lastError, ct);
    }

    public async Task MarkDeadLetteredAsync(Guid id, string lastError, CancellationToken ct)
    {
        await UpdateInternalAsync(id, NotificationDispatchStatus.DeadLettered, null, lastError, ct);
    }

    private async Task UpdateInternalAsync(Guid id, NotificationDispatchStatus status, string? queueMessageId, string? lastError, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = @"
            UPDATE NotificationOutbox 
            SET Status = @status, 
                QueueMessageId = ISNULL(@queueMessageId, QueueMessageId),
                LastError = @lastError,
                UpdatedUtc = @now 
            WHERE Id = @id";
        
        updateCmd.Parameters.AddWithValue("@id", id);
        updateCmd.Parameters.AddWithValue("@status", status.ToString());
        updateCmd.Parameters.AddWithValue("@queueMessageId", queueMessageId ?? (object)DBNull.Value);
        updateCmd.Parameters.AddWithValue("@lastError", lastError ?? (object)DBNull.Value);
        updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

        await updateCmd.ExecuteNonQueryAsync(ct);
    }
}
