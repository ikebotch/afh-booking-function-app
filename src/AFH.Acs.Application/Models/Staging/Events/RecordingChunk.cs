namespace AFH.Acs.Recorder.Models.Events;

public sealed class RecordingChunk
{
    public string ContentLocation { get; set; } = default!;
    public string MetadataLocation { get; set; } = default!;
}