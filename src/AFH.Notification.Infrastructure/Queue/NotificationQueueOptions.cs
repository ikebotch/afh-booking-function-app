namespace AFH.Notification.Infrastructure.Queue;

public sealed class NotificationQueueOptions
{
    public const string SectionName = "NotificationQueue";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "notifications-send";
}
