namespace AFH.Acs.Contract.V1.Responses;

public sealed class TranscriptionContentResponse
{
    public string JobId { get; init; } = string.Empty;

    public string? TranscriptFileName { get; init; }

    public string? TranscriptFileUrl { get; init; }

    public string? TranscriptText { get; init; }

    public string? SpeakerFormattedTranscript { get; init; }
}
