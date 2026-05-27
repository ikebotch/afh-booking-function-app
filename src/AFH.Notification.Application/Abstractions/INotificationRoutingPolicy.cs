using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationRoutingPolicy
{
    bool CanHandle(NotificationRequested notification);

    NotificationRoute Resolve(NotificationRequested notification);
}
