using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationOutboxStore
{
    Task<NotificationOutboxItem> CreateOrGetAsync(NotificationOutboxItem item, CancellationToken ct);
    Task<NotificationOutboxItem?> GetAsync(Guid id, CancellationToken ct);
    Task MarkQueuedAsync(Guid id, string queueMessageId, CancellationToken ct);
    Task MarkProcessingAsync(Guid id, CancellationToken ct);
    Task MarkSentAsync(Guid id, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string lastError, CancellationToken ct);
    Task MarkDeadLetteredAsync(Guid id, string lastError, CancellationToken ct);
}
