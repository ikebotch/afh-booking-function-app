namespace AFH.Acs.Contract.V1.Responses;

public sealed class TranscriptionFilesResponse
{
    public string JobId { get; init; } = string.Empty;

    public TranscriptionFileResponse? PrimaryTranscriptFile { get; init; }

    public IReadOnlyList<TranscriptionFileResponse> Files { get; init; } = [];
}
