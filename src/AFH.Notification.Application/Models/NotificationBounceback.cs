namespace AFH.Notification.Application.Models;

public sealed record NotificationBounceback(
    string ProviderMessageId,
    string Status,
    string? BounceReason,
    DateTime TimestampUtc);
