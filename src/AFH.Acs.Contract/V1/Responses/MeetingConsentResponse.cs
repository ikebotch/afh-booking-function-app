namespace AFH.Acs.Contract.V1.Responses;

public sealed class MeetingConsentResponse
{
    public string MeetingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public bool ConsentToRecording { get; init; }
    public DateTimeOffset ConsentTimestampUtc { get; init; }
}
