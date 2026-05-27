using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationQueuePublisher
{
    Task<NotificationQueuePublishResult> PublishAsync(NotificationQueueMessage message, CancellationToken ct);
}
