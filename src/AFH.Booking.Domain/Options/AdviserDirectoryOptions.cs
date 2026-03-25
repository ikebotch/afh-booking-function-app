namespace AFH.Booking.Domain.Options;

public sealed class AdviserDirectoryOptions
{
    public const string SectionName = "AdviserDirectory";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string CoverageEndpointPath { get; set; } = "/api/v1/admin/adviser-coverage";
    public string? InternalToken { get; set; }
    public int SyncIntervalMinutes { get; set; } = 30;
    public int SubscriptionRenewalLeadMinutes { get; set; } = 180;
    public bool AllowNonEmailMailboxIds { get; set; }
}
