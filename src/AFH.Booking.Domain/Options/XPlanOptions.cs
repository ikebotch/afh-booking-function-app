namespace AFH.Booking.Domain.Options;

public sealed class XPlanOptions
{
    public const string SectionName = "XPlan";

    public bool Enabled { get; set; } = false;
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}
