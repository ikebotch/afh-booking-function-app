using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationService
{
    Task PublishAsync(NotificationRequested notification, CancellationToken ct);
}
