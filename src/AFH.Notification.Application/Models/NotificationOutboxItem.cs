namespace AFH.Notification.Application.Models;

public sealed record NotificationOutboxItem(
    Guid Id,
    string SourceApplication,
    string NotificationType,
    string IdempotencyKey,
    string PayloadJson,
    NotificationDispatchStatus Status,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
