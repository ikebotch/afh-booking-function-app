using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationDeliveryAuditStore
{
    Task RecordAttemptAsync(NotificationDeliveryAuditRecord record, CancellationToken ct);
}
