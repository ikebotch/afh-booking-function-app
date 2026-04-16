namespace AFH.Acs.Recorder.Models.Events;


public sealed class RecordingStorageInfo
{
    public IReadOnlyList<RecordingChunk> RecordingChunks { get; set; } = Array.Empty<RecordingChunk>();
}