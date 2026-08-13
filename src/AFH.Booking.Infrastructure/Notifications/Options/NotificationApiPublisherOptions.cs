namespace AFH.Booking.Infrastructure.Notifications.Options;

public sealed class NotificationApiPublisherOptions
{
    public const string SectionName = "Notifications:Integration:Http";

    public string? BaseUrl { get; set; }
    public string RequestPath { get; set; } = "/api/v1/notifications/requests";
    public int TimeoutSeconds { get; set; } = 30;
    public string? FunctionKey { get; set; }
    public string? InternalToken { get; set; }
}
