namespace AFH.Booking.Application.Calendar.Options;

public sealed class GraphWebhookOptions
{
    public string? NotificationUrl { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public string? ClientState { get; set; }
}