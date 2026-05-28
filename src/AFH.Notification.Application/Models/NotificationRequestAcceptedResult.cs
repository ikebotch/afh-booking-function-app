namespace AFH.Notification.Application.Models;

public sealed record NotificationRequestAcceptedResult(
    Guid NotificationRequestId,
    string Status,
    string CorrelationId,
    bool Created);
