namespace AFH.Notification.Infrastructure.Integration.Inbound;

public sealed class NotificationInboundServiceBusOptions
{
    public const string SectionName = "Notifications:Inbound:ServiceBus";

    public bool Enabled { get; set; }
    public string? FullyQualifiedNamespace { get; set; }
    public string? ConnectionString { get; set; }
    public string TopicName { get; set; } = "notification-requests";
    public string SubscriptionName { get; set; } = "notification-service";
    public string? QueueName { get; set; }
}
