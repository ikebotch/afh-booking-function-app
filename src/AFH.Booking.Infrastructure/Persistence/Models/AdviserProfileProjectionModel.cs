namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class AdviserProfileProjectionModel
{
    public string AdviserId { get; set; } = default!;
    public string DisplayName { get; set; } = string.Empty;
    public string MailboxUserId { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string HomePostcode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public double Rating { get; set; }
    public string SkillsJson { get; set; } = "[]";
    public double? CoverageRadiusMiles { get; set; }
    public int? MaxTravelTimeMinutes { get; set; }
    public DateTime LastSyncedUtc { get; set; }
    public string? SourceVersion { get; set; }
}
