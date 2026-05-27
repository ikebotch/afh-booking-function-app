namespace AFH.Notification.Application.Models;

public sealed record NotificationBouncebackResult(
    bool IsSuccess,
    string? ErrorMessage,
    int ProcessedCount = 0,
    string? ValidationResponse = null);
