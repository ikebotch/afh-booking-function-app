namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class PartnerWorkflowEndpointModel
{
    public string PartnerKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public bool Enabled { get; set; }
    public string? BookingUpdatesUrl { get; set; }
    public string? BaseUrl { get; set; }
    public string BookingUpdatesPath { get; set; } = "/api/booking-updates";
    public string? ApiKey { get; set; }
    public string ApiKeyHeaderName { get; set; } = "Authorization";
    public string IdempotencyKeyHeaderName { get; set; } = "X-Idempotency-Key";
    public string PayloadFormat { get; set; } = "LegacyWrapper";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
