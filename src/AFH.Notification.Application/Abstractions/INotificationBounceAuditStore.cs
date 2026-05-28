using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationBounceAuditStore
{
    Task<NotificationBounceAuditResult> RecordAsync(
        NotificationBounceAuditRecord record,
        CancellationToken ct);
}
