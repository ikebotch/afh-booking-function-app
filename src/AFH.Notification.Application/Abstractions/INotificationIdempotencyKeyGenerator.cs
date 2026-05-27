using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationIdempotencyKeyGenerator
{
    string GenerateKey(NotificationRequested request, NotificationChannel channel, NotificationRecipient recipient);
}
