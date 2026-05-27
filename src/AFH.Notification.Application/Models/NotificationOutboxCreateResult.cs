namespace AFH.Notification.Application.Models;

public sealed record NotificationOutboxCreateResult(
    NotificationOutboxItem Item,
    bool Created);
