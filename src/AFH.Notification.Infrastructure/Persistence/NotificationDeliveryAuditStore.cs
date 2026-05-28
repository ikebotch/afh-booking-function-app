using System.Security.Cryptography;
using System.Text;
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
        var dispatch = new NotificationDispatchModel
        {
            Id = record.Id,
            DispatchUid = record.DispatchUid,
            CorrelationId = Truncate(record.CorrelationId, 150),
            FailureDetails = record.FailureDetails,
            RecipientType = Truncate(record.RecipientType, 100),
            RecipientEmail = Truncate(record.RecipientEmail, 320),
            ProviderMessageId = Truncate(record.ProviderMessageId, 200),
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
        };

        await _db.NotificationDispatches.AddAsync(dispatch, ct);

        if (record.MessageLog is not null)
            await _db.NotificationMessageLogs.AddAsync(MapMessageLog(record.MessageLog), ct);

        await _db.SaveChangesAsync(ct);
    }

    private static NotificationMessageLogModel MapMessageLog(NotificationMessageLogRecord record)
        => new()
        {
            Id = record.Id,
            NotificationDispatchId = record.NotificationDispatchId,
            NotificationOutboxId = record.NotificationOutboxId,
            SourceApplication = Truncate(record.SourceApplication, 100),
            NotificationType = Truncate(record.NotificationType, 150),
            CorrelationId = Truncate(record.CorrelationId, 150),
            RecipientType = Truncate(record.RecipientType, 100),
            RecipientEmail = Truncate(record.RecipientEmail, 320),
            RecipientMobile = Truncate(record.RecipientMobile, 50),
            Channel = TruncateRequired(record.Channel, 50),
            TemplateKey = TruncateRequired(record.TemplateKey, 150),
            TemplateVersion = TruncateRequired(record.TemplateVersion, 50),
            TemplateContentId = record.TemplateContentId,
            Subject = Truncate(record.Subject, 500),
            Body = record.Body,
            ContentType = TruncateRequired(record.ContentType, 50),
            RenderDataJson = record.RenderDataJson,
            BodyHash = Truncate(record.BodyHash, 128) ?? ComputeSha256(record.Body),
            CreatedUtc = record.CreatedUtc
        };

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string TruncateRequired(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
