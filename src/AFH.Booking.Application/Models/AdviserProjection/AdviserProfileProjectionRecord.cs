namespace AFH.Booking.Application.Models.AdviserProjection;

public sealed class AdviserProfileProjectionRecord
{
    public string AdviserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string MailboxUserId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string HomePostcode { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public double Rating { get; init; }
    public IReadOnlyList<string> Skills { get; init; } = Array.Empty<string>();
    public double? CoverageRadiusMiles { get; init; }
    public int? MaxTravelTimeMinutes { get; init; }
    public DateTime LastSyncedUtc { get; init; }
    public string? SourceVersion { get; init; }
}
