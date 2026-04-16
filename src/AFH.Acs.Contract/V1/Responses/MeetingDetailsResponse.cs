namespace AFH.Acs.Contract.V1.Responses;

public sealed class MeetingDetailsResponse
{
    public string MeetingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string AdviserId { get; init; } = string.Empty;
    public string? AdviserName { get; init; }
    public string LeadId { get; init; } = string.Empty;
    public string MeetingType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string ClientEmail { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public bool ConsentToRecording { get; init; }
    public DateTimeOffset? ConsentTimestampUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<MeetingAttendeeResponse> Attendees { get; init; } = [];
    public IReadOnlyList<MeetingRecordingResponse> Recordings { get; init; } = [];
    public MeetingTranscriptionResponse? Transcription { get; init; }
}
