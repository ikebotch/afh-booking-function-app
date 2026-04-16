namespace AFH.Acs.Contract.V1.Responses;

public sealed class MeetingRecordingResponse
{
    public string RecordingId { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public DateTimeOffset RecordingStartUtc { get; set; }
    public DateTimeOffset? RecordingEndUtc { get; set; }
    public int? DurationSeconds { get; set; }
}
