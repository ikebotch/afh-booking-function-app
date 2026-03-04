namespace AFH.Booking.Infrastructure.Options;

public sealed class GraphWebhookOptions
{
    public string? NotificationUrl { get; set; }   // https://.../api/v1/calendar/notifications
    public string? ClientState { get; set; }       // shared secret value
    public int MaxSubscriptionMinutes { get; set; } = 4320; // example
}