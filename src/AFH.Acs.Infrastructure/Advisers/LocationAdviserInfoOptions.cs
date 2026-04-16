namespace AFH.Acs.Infrastructure.Advisers;

public sealed class LocationAdviserInfoOptions
{
    public const string SectionName = "Location";

    public string BaseUrl { get; set; } = string.Empty;
    public string CoveragePath { get; set; } = "/api/v1/admin/adviser-coverage";
    public string? FunctionCode { get; set; }
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
