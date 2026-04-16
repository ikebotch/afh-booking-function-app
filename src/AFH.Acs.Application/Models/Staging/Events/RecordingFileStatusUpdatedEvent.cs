namespace AFH.Acs.Recorder.Models.Events;

public sealed class RecordingFileStatusUpdatedEvent
{
    public string RecordingId { get; set; } = default!;
    public string? SessionId { get; set; }
    public string Status { get; set; } = default!;

    public RecordingStorageInfo RecordingStorageInfo { get; set; } = default!;
}