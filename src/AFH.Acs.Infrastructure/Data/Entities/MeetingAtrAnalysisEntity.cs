namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class MeetingAtrAnalysisEntity
{
    public string MeetingId { get; set; } = default!;
    public string ClientAtrText { get; set; } = default!;        // free text
    public string MatchedTemplateIdsJson { get; set; } = "[]";   // JSON array
    public string MissingKeypointsJson { get; set; } = "[]";     // JSON array
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public MeetingEntity Meeting { get; set; } = default!;
}