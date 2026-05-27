using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationIdempotencyPolicy
{
    bool CanHandle(NotificationRequested request);

    string GetPrimaryId(NotificationRequested request);
}
