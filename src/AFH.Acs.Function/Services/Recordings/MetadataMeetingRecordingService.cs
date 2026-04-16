using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Function.Services.Meetings;

namespace AFH.Acs.Function.Services.Recordings;

public sealed class MetadataMeetingRecordingService(IMeetingWorkflowStore meetings) : IMeetingRecordingService
{
    public Task<MeetingRecordingResponse> StartRecordingAsync(StartRecordingRequest request, CancellationToken ct = default)
        => meetings.StartRecordingAsync(request, ct);

    public Task<MeetingRecordingResponse> StopRecordingAsync(StopRecordingRequest request, CancellationToken ct = default)
        => meetings.StopRecordingAsync(request, ct);

    public Task<IReadOnlyList<MeetingRecordingResponse>> ListRecordingsAsync(string? meetingId, CancellationToken ct = default)
        => meetings.ListRecordingsAsync(meetingId, ct);

    public Task<MeetingRecordingResponse?> GetRecordingAsync(string recordingId, CancellationToken ct = default)
        => meetings.GetRecordingAsync(recordingId, ct);
}
