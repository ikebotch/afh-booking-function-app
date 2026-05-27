namespace AFH.Notification.Infrastructure.Queue;

public sealed class NotificationQueueOptions
{
    public const string SectionName = "Notifications:Queue";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "notifications-send";
}
