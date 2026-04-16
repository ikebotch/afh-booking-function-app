namespace AFH.Acs.Domain.Entities;

public sealed class MeetingTranscriptionArtifact
{
    public string TranscriptionId { get; init; } = string.Empty;
    public string Language { get; init; } = "en-GB";
    public string FullText { get; init; } = string.Empty;
    public string? SummaryText { get; init; }
}
