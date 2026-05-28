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
        await _db.NotificationDispatches.AddAsync(new NotificationDispatchModel
        {
            Id = record.Id,
            CorrelationId = Truncate(record.CorrelationId, 150),
            FailureDetails = record.FailureDetails,
            RecipientType = Truncate(record.RecipientType, 100),
            RecipientEmail = Truncate(record.RecipientEmail, 320),
            ProviderMessageId = Truncate(record.ProviderMessageId, 200),
            MessageSubject = Truncate(record.MessageSubject, 500),
            MessageBody = record.MessageBody,
            CreatedUtc = record.CreatedUtc,
            UpdatedUtc = record.UpdatedUtc,
            NotificationOutboxId = record.NotificationOutboxId,
            SourceApplication = TruncateRequired(record.SourceApplication, 100),
            SourceReferenceType = Truncate(record.SourceReferenceType, 100),
            SourceReferenceId = Truncate(record.SourceReferenceId, 150),
            NotificationType = TruncateRequired(record.NotificationType, 150),
            RecipientMobile = Truncate(record.RecipientMobile, 50),
            Channel = TruncateRequired(record.Channel, 50),
            ProviderName = TruncateRequired(record.ProviderName, 100),
            TemplateKey = Truncate(record.TemplateKey, 150),
            TemplateVersion = Truncate(record.TemplateVersion, 50),
            Status = TruncateRequired(record.Status, 50),
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
