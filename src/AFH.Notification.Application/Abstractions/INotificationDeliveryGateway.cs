using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationDeliveryGateway
{
    bool CanSend(NotificationChannel channel);
    Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct);
}
