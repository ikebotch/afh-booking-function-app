using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationBouncebackStore
{
    Task RecordBouncebackAsync(
        NotificationBounceback bounceback,
        CancellationToken ct);
}
