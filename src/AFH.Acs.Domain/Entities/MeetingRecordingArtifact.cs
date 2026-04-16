namespace AFH.Acs.Domain.Entities;

public sealed class MeetingRecordingArtifact
{
    public string RecordingId { get; init; } = string.Empty;
    public string BlobName { get; init; } = string.Empty;
    public string BlobUrl { get; init; } = string.Empty;
    public DateTimeOffset RecordingStartUtc { get; init; }
    public DateTimeOffset? RecordingEndUtc { get; init; }
    public int? DurationSeconds { get; init; }
}
