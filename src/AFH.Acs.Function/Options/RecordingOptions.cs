namespace AFH.Acs.Function.Options;

public sealed class RecordingOptions
{
    public const string SectionName = "Recording";

    public RecordingMode Mode { get; set; } = RecordingMode.Metadata;
}

public enum RecordingMode
{
    Metadata = 0,
    LiveAcs = 1
}
