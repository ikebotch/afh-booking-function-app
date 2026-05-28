namespace AFH.Notification.Application.Models;

public sealed record NotificationAdminOperationResult(
    Guid NotificationRequestId,
    string Status,
    string? QueueMessageId = null);
