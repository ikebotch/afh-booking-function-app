using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationOutboxStore
{
    Task<NotificationOutboxItem> SaveAsync(NotificationOutboxItem item, CancellationToken ct);
    Task<NotificationOutboxItem?> GetAsync(Guid id, CancellationToken ct);
    Task UpdateStatusAsync(Guid id, NotificationDispatchStatus status, string? lastError, CancellationToken ct);
}
