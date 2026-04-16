namespace AFH.Acs.Contract.V1.Responses;

public sealed class TranscriptionJobResponse
{
    public string? MeetingId { get; init; }

    public string JobId { get; init; } = string.Empty;

    public string? Status { get; init; }

    public string? DisplayName { get; init; }

    public DateTimeOffset? CreatedDateTime { get; init; }

    public DateTimeOffset? LastActionDateTime { get; init; }

    public string? Locale { get; init; }

    public string? Model { get; init; }

    public string? SourceUrl { get; init; }
}
