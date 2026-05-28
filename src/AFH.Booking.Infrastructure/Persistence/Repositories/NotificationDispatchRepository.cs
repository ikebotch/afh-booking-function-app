using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class NotificationDispatchRepository : INotificationDispatchRepository
{
    private readonly INotificationDeliveryAuditStore _auditStore;

    public NotificationDispatchRepository(INotificationDeliveryAuditStore auditStore)
    {
        _auditStore = auditStore;
    }

    public Task AddAsync(NotificationDispatchRecord record, CancellationToken ct)
        => _auditStore.RecordAttemptAsync(new NotificationDeliveryAuditRecord(
            record.Id,
            record.NotificationOutboxId,
            record.SourceApplication ?? "Booking",
            "Booking",
            record.BookingId,
            record.NotificationType ?? record.EventType,
            record.Channel ?? "Email",
            null,
            record.RecipientEmail,
            record.RecipientPhone,
            record.ProviderName ?? "Recorded",
            record.OutcomeCode,
            record.ProviderMessageId,
            record.FailureDetails,
            record.CorrelationId,
            record.TemplateKey,
            record.TemplateVersion,
            record.CreatedUtc,
            record.UpdatedUtc,
            MessageBody: record.MessageBody), ct);
}
