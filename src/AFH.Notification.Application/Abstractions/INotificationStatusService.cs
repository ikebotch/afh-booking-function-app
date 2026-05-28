using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationStatusService
{
    Task<NotificationRequestStatus?> GetRequestAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<NotificationRequestSummary>> QueryRequestsAsync(NotificationRequestQuery query, CancellationToken ct);
    Task<NotificationDispatchSummary?> GetDispatchAsync(string id, CancellationToken ct);
    Task<NotificationMessageLogDetail?> GetMessageLogAsync(Guid id, CancellationToken ct);
}
