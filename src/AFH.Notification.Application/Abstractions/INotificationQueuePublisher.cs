using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationQueuePublisher
{
    Task PublishAsync(NotificationQueueMessage message, CancellationToken ct);
}
