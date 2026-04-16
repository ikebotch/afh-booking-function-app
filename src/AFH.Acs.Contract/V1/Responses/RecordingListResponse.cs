namespace AFH.Acs.Contract.V1.Responses;

public sealed class RecordingListResponse
{
    public string? MeetingId { get; init; }
    public IReadOnlyList<MeetingRecordingResponse> Items { get; init; } = [];
}
