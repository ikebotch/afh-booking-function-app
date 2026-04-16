namespace AFH.Acs.Contract.V1.Requests;

public sealed class SubmitTranscriptionRequest
{
    public string ContentUrl { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? Locale { get; init; }

    public TranscriptionJobSettings? Settings { get; init; }
}

public sealed class TranscriptionJobSettings
{
    public bool? DiarizationEnabled { get; init; }

    public bool? WordLevelTimestampsEnabled { get; init; }
}
