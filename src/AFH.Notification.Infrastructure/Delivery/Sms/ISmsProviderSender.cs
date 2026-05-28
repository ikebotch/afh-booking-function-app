using AFH.Notification.Application.Models;

namespace AFH.Notification.Infrastructure.Delivery.Sms;

public interface ISmsProviderSender
{
    Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct);
}
