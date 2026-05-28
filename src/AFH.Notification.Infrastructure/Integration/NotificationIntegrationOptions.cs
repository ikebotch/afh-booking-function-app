namespace AFH.Notification.Infrastructure.Integration;

public sealed class NotificationIntegrationOptions
{
    public const string SectionName = "Notifications:Integration";

    public string Transport { get; set; } = "Http";
}
