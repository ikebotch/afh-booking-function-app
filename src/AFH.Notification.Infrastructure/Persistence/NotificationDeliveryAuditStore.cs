using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationDeliveryAuditStore : INotificationDeliveryAuditStore
{
    private readonly string _connectionString;

    public NotificationDeliveryAuditStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? configuration["Values:ConnectionStrings:BookingDb"]
            ?? configuration["Values:BookingDb:ConnectionString"]
            ?? throw new InvalidOperationException("BookingDb connection string is not configured.");
    }

    public async Task RecordAttemptAsync(NotificationDeliveryAuditRecord record, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO NotificationDispatches
(
    Id,
    BookingId,
    TransactionId,
    TransactionRef,
    LifecycleEventId,
    CorrelationId,
    EventType,
    SmsRequested,
    EmailRequested,
    SmsStatus,
    EmailStatus,
    OutcomeCode,
    FailureDetails,
    RecipientPhone,
    RecipientEmail,
    ProviderMessageId,
    MessageBody,
    CreatedUtc,
    UpdatedUtc,
    NotificationOutboxId,
    SourceApplication,
    NotificationType,
    Channel,
    ProviderName,
    TemplateName
)
VALUES
(
    @id,
    @bookingId,
    @transactionId,
    @transactionRef,
    NULL,
    @correlationId,
    @eventType,
    @smsRequested,
    @emailRequested,
    @smsStatus,
    @emailStatus,
    @outcomeCode,
    @failureDetails,
    @recipientPhone,
    @recipientEmail,
    @providerMessageId,
    NULL,
    @createdUtc,
    @updatedUtc,
    @notificationOutboxId,
    @sourceApplication,
    @notificationType,
    @channel,
    @providerName,
    @templateName
)";

        var isEmail = string.Equals(record.Channel, "Email", StringComparison.OrdinalIgnoreCase);
        var bookingId = string.IsNullOrWhiteSpace(record.BookingId)
            ? record.CorrelationId ?? record.NotificationOutboxId?.ToString("N") ?? record.Id
            : record.BookingId;

        command.Parameters.AddWithValue("@id", record.Id);
        command.Parameters.AddWithValue("@bookingId", TruncateRequired(bookingId, 64));
        command.Parameters.AddWithValue("@transactionId", DbValue(Truncate(record.TransactionId, 64)));
        command.Parameters.AddWithValue("@transactionRef", DbValue(Truncate(record.TransactionRef, 128)));
        command.Parameters.AddWithValue("@correlationId", DbValue(Truncate(record.CorrelationId, 128)));
        command.Parameters.AddWithValue("@eventType", TruncateRequired(record.NotificationType, 64));
        command.Parameters.AddWithValue("@smsRequested", !isEmail);
        command.Parameters.AddWithValue("@emailRequested", isEmail);
        command.Parameters.AddWithValue("@smsStatus", isEmail ? "Skipped" : TruncateRequired(record.Status, 32));
        command.Parameters.AddWithValue("@emailStatus", isEmail ? TruncateRequired(record.Status, 32) : "Skipped");
        command.Parameters.AddWithValue("@outcomeCode", TruncateRequired(record.Status, 64));
        command.Parameters.AddWithValue("@failureDetails", DbValue(Truncate(record.FailureDetails, 2048)));
        command.Parameters.AddWithValue("@recipientPhone", DbValue(Truncate(record.RecipientPhone, 64)));
        command.Parameters.AddWithValue("@recipientEmail", DbValue(Truncate(record.RecipientEmail, 256)));
        command.Parameters.AddWithValue("@providerMessageId", DbValue(Truncate(record.ProviderMessageId, 128)));
        command.Parameters.AddWithValue("@createdUtc", record.CreatedUtc);
        command.Parameters.AddWithValue("@updatedUtc", record.UpdatedUtc);
        command.Parameters.AddWithValue("@notificationOutboxId", record.NotificationOutboxId.HasValue ? record.NotificationOutboxId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@sourceApplication", TruncateRequired(record.SourceApplication, 100));
        command.Parameters.AddWithValue("@notificationType", TruncateRequired(record.NotificationType, 150));
        command.Parameters.AddWithValue("@channel", TruncateRequired(record.Channel, 50));
        command.Parameters.AddWithValue("@providerName", TruncateRequired(record.ProviderName, 100));
        command.Parameters.AddWithValue("@templateName", DbValue(Truncate(record.TemplateName, 200)));

        await command.ExecuteNonQueryAsync(ct);
    }

    private static object DbValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static string TruncateRequired(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
