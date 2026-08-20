namespace AFH.Booking.Domain.Options;

public sealed class XPlanOptions
{
    public const string SectionName = "XPlan";

    public bool Enabled { get; set; } = false;
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class PartnerWorkflowOptions
{
    public const string SectionName = "PartnerWorkflow";

    public bool Enabled { get; set; } = false;
    public string? BookingUpdatesUrl { get; set; }
    public string? BaseUrl { get; set; }
    public string BookingUpdatesPath { get; set; } = "/api/booking-updates";
    public string? ApiKey { get; set; }
    public string ApiKeyHeaderName { get; set; } = "Authorization";
    public string IdempotencyKeyHeaderName { get; set; } = "X-Idempotency-Key";
    public string PayloadFormat { get; set; } = "LegacyWrapper";
}
