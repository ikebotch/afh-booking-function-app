namespace AFH.Notification.Infrastructure.Options;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = true;
}
