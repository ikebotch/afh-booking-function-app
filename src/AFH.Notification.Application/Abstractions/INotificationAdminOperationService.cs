using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationAdminOperationService
{
    Task<NotificationAdminOperationResult> RequeueAsync(Guid id, CancellationToken ct);
    Task<NotificationAdminOperationResult> DeadLetterAsync(Guid id, string reason, CancellationToken ct);
    Task<NotificationAdminOperationResult> MarkFailedAsync(Guid id, string reason, CancellationToken ct);
}
