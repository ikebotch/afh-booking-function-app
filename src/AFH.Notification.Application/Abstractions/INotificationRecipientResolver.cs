using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationRecipientResolver
{
    Task<NotificationRoute> ResolveAsync(NotificationRequested notification, CancellationToken ct);
}
