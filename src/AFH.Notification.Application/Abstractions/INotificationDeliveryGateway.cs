using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationDeliveryGateway
{
    Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct);
}
