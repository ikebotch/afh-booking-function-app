namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class MeetingTranscriptionEntity
{
    public string TranscriptionId { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public string Language { get; set; } = "en-GB";
    public string FullText { get; set; } = string.Empty;
    public string? SummaryText { get; set; }
    public MeetingEntity Meeting { get; set; } = default!;
}
