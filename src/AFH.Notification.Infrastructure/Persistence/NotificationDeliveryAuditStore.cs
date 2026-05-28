using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence.Models;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationDeliveryAuditStore : INotificationDeliveryAuditStore
{
    private readonly NotificationDbContext _db;

    public NotificationDeliveryAuditStore(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task RecordAttemptAsync(NotificationDeliveryAuditRecord record, CancellationToken ct)
    {
        var isEmail = string.Equals(record.Channel, "Email", StringComparison.OrdinalIgnoreCase);
        var bookingId = string.IsNullOrWhiteSpace(record.BookingId)
            ? record.CorrelationId ?? record.NotificationOutboxId?.ToString("N") ?? record.Id
            : record.BookingId;

        await _db.NotificationDispatches.AddAsync(new NotificationDispatchModel
        {
            Id = record.Id,
            BookingId = TruncateRequired(bookingId, 64),
            TransactionId = Truncate(record.TransactionId, 64),
            TransactionRef = Truncate(record.TransactionRef, 128),
            CorrelationId = Truncate(record.CorrelationId, 150),
            EventType = TruncateRequired(record.NotificationType, 64),
            SmsRequested = !isEmail,
            EmailRequested = isEmail,
            SmsStatus = isEmail ? "Skipped" : TruncateRequired(record.Status, 32),
            EmailStatus = isEmail ? TruncateRequired(record.Status, 32) : "Skipped",
            OutcomeCode = TruncateRequired(record.Status, 64),
            FailureDetails = record.FailureDetails,
            RecipientType = Truncate(record.RecipientType, 100),
            RecipientPhone = Truncate(record.RecipientPhone, 64),
            RecipientEmail = Truncate(record.RecipientEmail, 320),
            ProviderMessageId = Truncate(record.ProviderMessageId, 200),
            MessageSubject = Truncate(record.MessageSubject, 500),
            MessageBody = record.MessageBody,
            CreatedUtc = record.CreatedUtc,
            UpdatedUtc = record.UpdatedUtc,
            NotificationOutboxId = record.NotificationOutboxId,
            SourceApplication = TruncateRequired(record.SourceApplication, 100),
            NotificationType = TruncateRequired(record.NotificationType, 150),
            Channel = TruncateRequired(record.Channel, 50),
            ProviderName = TruncateRequired(record.ProviderName, 100),
            TemplateName = Truncate(record.TemplateKey, 200),
            TemplateKey = Truncate(record.TemplateKey, 150),
            TemplateVersion = Truncate(record.TemplateVersion, 50),
            CompletedUtc = string.Equals(record.Status, "Failed", StringComparison.OrdinalIgnoreCase) ? null : record.UpdatedUtc
        }, ct);

        await _db.SaveChangesAsync(ct);
    }

    private static string TruncateRequired(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
