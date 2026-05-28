namespace AFH.Notification.Infrastructure.Integration;

public sealed class ServiceBusNotificationPublisherOptions
{
    public const string SectionName = "Notifications:Integration:ServiceBus";

    public string? FullyQualifiedNamespace { get; set; }
    public string? ConnectionString { get; set; }
    public string TopicName { get; set; } = "notification-requests";
    public string? QueueName { get; set; }
}
