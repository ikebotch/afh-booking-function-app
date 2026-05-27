namespace AFH.Notification.Application.Models;

public sealed record NotificationOutboxItem(
    Guid Id,
    string SourceApplication,
    string NotificationType,
    string IdempotencyKey,
    string PayloadJson,
    NotificationDispatchStatus Status,
    string? QueueMessageId,
    int AttemptCount,
    string? LastError,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    DateTime? ProcessedUtc);
