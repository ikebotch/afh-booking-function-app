namespace AFH.Notification.Infrastructure.Queue;

public sealed class NotificationQueueOptions
{
    public const string SectionName = "Notifications:Queue";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "notifications-send";

    public void ValidateForAzureQueueMode()
    {
        if (string.IsNullOrWhiteSpace(QueueName))
            throw new InvalidOperationException("Notifications:Queue:QueueName is required for hybrid notification dispatch.");

        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("Notifications:Queue:ConnectionString is required for hybrid notification dispatch.");
    }
}
