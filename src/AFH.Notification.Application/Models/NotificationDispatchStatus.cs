namespace AFH.Notification.Application.Models;

public enum NotificationDispatchStatus
{
    Pending = 1,
    Queued = 2,
    Processing = 3,
    Sent = 4,
    Failed = 5,
    DeadLettered = 6,
    SkippedDuplicate = 7
}
