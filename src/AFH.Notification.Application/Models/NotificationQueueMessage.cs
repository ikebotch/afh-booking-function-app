namespace AFH.Notification.Application.Models;

public sealed record NotificationQueueMessage
{
    public required Guid OutboxId { get; init; }
}
