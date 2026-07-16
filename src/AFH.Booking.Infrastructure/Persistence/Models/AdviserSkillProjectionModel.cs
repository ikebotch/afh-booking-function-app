namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class AdviserSkillProjectionModel
{
    public string AdviserId { get; set; } = string.Empty;
    public string SkillCode { get; set; } = string.Empty;
    public string SkillLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public DateTime LastSyncedUtc { get; set; }
    public string? SourceVersion { get; set; }
}
