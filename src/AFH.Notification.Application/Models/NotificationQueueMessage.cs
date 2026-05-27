namespace AFH.Notification.Application.Models;

public sealed record NotificationQueueMessage
{
    public required Guid NotificationOutboxId { get; init; }
    public required string SourceApplication { get; init; }
    public required string NotificationType { get; init; }
}
