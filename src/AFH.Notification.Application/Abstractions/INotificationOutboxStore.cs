using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationOutboxStore
{
    Task<NotificationOutboxCreateResult> CreateOrGetAsync(NotificationOutboxItem item, CancellationToken ct);
    Task<NotificationOutboxItem?> GetAsync(Guid id, CancellationToken ct);
    Task MarkQueuedAsync(Guid id, string queueMessageId, CancellationToken ct);
    Task<NotificationOutboxItem?> TryMarkProcessingAsync(Guid id, DateTime utcNow, TimeSpan processingLock, CancellationToken ct);
    Task MarkSentAsync(Guid id, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string lastError, DateTime nextAttemptUtc, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string lastError, CancellationToken ct);
    Task MarkDeadLetteredAsync(Guid id, string lastError, CancellationToken ct);
}
