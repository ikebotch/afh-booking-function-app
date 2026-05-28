using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationRequestIngestionService
{
    Task<NotificationRequestAcceptedResult> AcceptAsync(NotificationRequested request, CancellationToken ct);
}
