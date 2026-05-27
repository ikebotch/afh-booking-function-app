using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace AFH.Notification.Infrastructure.Bouncebacks;

public sealed class EmailBouncebackStore : INotificationBouncebackStore
{
    private readonly string _connectionString;
    private readonly ILogger<EmailBouncebackStore> _logger;

    public EmailBouncebackStore(IConfiguration configuration, ILogger<EmailBouncebackStore> logger)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? configuration["Values:ConnectionStrings:BookingDb"]
            ?? configuration["Values:BookingDb:ConnectionString"]
            ?? throw new InvalidOperationException("BookingDb connection string is not configured.");
        _logger = logger;
    }

    public async Task RecordBouncebackAsync(NotificationBounceback bounceback, CancellationToken ct)
    {
        _logger.LogInformation(
            "Recording bounceback for ProviderMessageId={ProviderMessageId}, Status={Status}, Reason={Reason}",
            bounceback.ProviderMessageId,
            bounceback.Status,
            bounceback.BounceReason);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            await using var updateCmd = connection.CreateCommand();
            updateCmd.Transaction = transaction;
            updateCmd.CommandText = @"
                UPDATE NotificationDispatches
                SET EmailStatus = @status,
                    FailureDetails = @reason,
                    UpdatedUtc = @now
                WHERE ProviderMessageId = @messageId";

            updateCmd.Parameters.AddWithValue("@status", bounceback.Status);
            updateCmd.Parameters.AddWithValue("@reason", bounceback.BounceReason ?? (object)DBNull.Value);
            updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            updateCmd.Parameters.AddWithValue("@messageId", bounceback.ProviderMessageId);

            await updateCmd.ExecuteNonQueryAsync(ct);

            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = @"
                INSERT INTO EmailBounceEvents (Id, ProviderMessageId, ReasonCode, ReasonDetail, OccurredUtc, ReceivedUtc)
                VALUES (@id, @messageId, @statusCode, @reason, @occurred, @now)";

            insertCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
            insertCmd.Parameters.AddWithValue("@messageId", bounceback.ProviderMessageId);
            insertCmd.Parameters.AddWithValue("@statusCode", bounceback.Status);
            insertCmd.Parameters.AddWithValue("@reason", bounceback.BounceReason ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@occurred", bounceback.TimestampUtc);
            insertCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

            await insertCmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist bounceback for message {MessageId}", bounceback.ProviderMessageId);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
