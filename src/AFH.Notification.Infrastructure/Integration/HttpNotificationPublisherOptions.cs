namespace AFH.Notification.Infrastructure.Integration;

public sealed class HttpNotificationPublisherOptions
{
    public const string SectionName = "Notifications:Integration:Http";

    public string? BaseUrl { get; set; }
    public string RequestPath { get; set; } = "/api/v1/notifications/requests";
    public int TimeoutSeconds { get; set; } = 30;
    public string? FunctionKey { get; set; }
    public string? InternalToken { get; set; }
}
