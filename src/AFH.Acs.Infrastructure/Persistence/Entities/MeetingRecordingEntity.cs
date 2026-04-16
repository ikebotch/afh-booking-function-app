namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class MeetingRecordingEntity
{
    public string RecordingId { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public DateTime RecordingStartUtc { get; set; }
    public DateTime? RecordingEndUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public MeetingEntity Meeting { get; set; } = default!;
}
