namespace AFH.Acs.Application.Abstractions.Transcription;

public sealed class SpeechTranscriptionStartRequest
{
    public required IReadOnlyList<Uri> ContentUrls { get; init; }
    public string? DisplayName { get; init; }
    public string? Locale { get; init; }
    public SpeechTranscriptionSettings? Settings { get; init; }
}

public sealed class SpeechTranscriptionSettings
{
    public bool? DiarizationEnabled { get; init; }
    public bool? WordLevelTimestampsEnabled { get; init; }
}

public sealed class SpeechTranscriptionJobStatus
{
    public string JobId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public DateTimeOffset? CreatedDateTime { get; init; }
    public DateTimeOffset? LastActionDateTime { get; init; }
    public string? Locale { get; init; }
    public string? Model { get; init; }
    public Uri? Self { get; init; }
}

public sealed class SpeechTranscriptionFile
{
    public string Name { get; init; } = string.Empty;
    public string? Kind { get; init; }
    public DateTimeOffset? CreatedDateTime { get; init; }
    public long? SizeInBytes { get; init; }
    public long? ContentLength { get; init; }
    public Uri? Self { get; init; }
    public Uri? ContentUrl { get; init; }
    public Uri? ContentUri { get; init; }
}

public sealed class SpeechTranscriptionFilesResult
{
    public IReadOnlyList<SpeechTranscriptionFile> Files { get; init; } = Array.Empty<SpeechTranscriptionFile>();
    public SpeechTranscriptionFile? PrimaryTranscriptFile { get; init; }
}

public sealed class SpeechTranscriptContent
{
    public string TranscriptText { get; init; } = string.Empty;
    public string SpeakerFormattedTranscript { get; init; } = string.Empty;
}
