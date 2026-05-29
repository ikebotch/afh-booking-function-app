namespace AFH.Notification.Infrastructure.Options;

public sealed class NotificationIntegrationOptions
{
    public const string SectionName = "Notifications:Integration";

    public string Transport { get; set; } = "InProcess";
}
