using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationAuditStore
{
    Task RecordRequestedAsync(NotificationRequested notification, CancellationToken ct);
}
