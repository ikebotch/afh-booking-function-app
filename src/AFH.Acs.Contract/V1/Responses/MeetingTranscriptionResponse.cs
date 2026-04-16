namespace AFH.Acs.Contract.V1.Responses;

public sealed class MeetingTranscriptionResponse
{
    public string TranscriptionId { get; init; } = string.Empty;
    public string Language { get; init; } = "en-GB";
    public string FullText { get; init; } = string.Empty;
    public string? SummaryText { get; init; }
}
