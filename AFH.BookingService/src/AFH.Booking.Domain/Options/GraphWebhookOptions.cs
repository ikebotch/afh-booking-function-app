namespace AFH.Booking.Infrastructure.Options;

public sealed class GraphWebhookOptions
{
    public const string SectionName = "GraphWebhook";

    public string NotificationUrl { get; set; } = default!;

    public string ClientState { get; set; } = default!;

    public int ExpirationMinutes { get; set; } = 60;
}