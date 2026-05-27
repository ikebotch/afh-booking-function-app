using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Contract.Abstractions;

public interface INotificationPublisher
{
    Task PublishAsync(NotificationRequested notification, CancellationToken ct);
}
