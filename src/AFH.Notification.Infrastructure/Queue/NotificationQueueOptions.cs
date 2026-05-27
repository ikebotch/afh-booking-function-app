namespace AFH.Notification.Infrastructure.Queue;

public sealed class NotificationQueueOptions
{
    public const string SectionName = "NotificationQueue";
    public const string LegacySectionName = "Notifications:Queue";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "notifications-send";

    public void ValidateForAzureQueueMode()
    {
        if (string.IsNullOrWhiteSpace(QueueName))
            throw new InvalidOperationException("NotificationQueue:QueueName is required for hybrid notification dispatch.");

        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("NotificationQueue:ConnectionString is required for hybrid notification dispatch.");
    }
}
