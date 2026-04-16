namespace AFH.Acs.Contract.V1.Requests;

public sealed class StartRecordingRequest
{
    public string? MeetingId { get; init; }
    public string? GroupId { get; init; }
    public string? BlobName { get; init; }
}
