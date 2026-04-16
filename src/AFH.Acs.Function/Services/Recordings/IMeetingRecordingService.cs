using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;

namespace AFH.Acs.Function.Services.Recordings;

public interface IMeetingRecordingService
{
    Task<MeetingRecordingResponse> StartRecordingAsync(StartRecordingRequest request, CancellationToken ct = default);
    Task<MeetingRecordingResponse> StopRecordingAsync(StopRecordingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingRecordingResponse>> ListRecordingsAsync(string? meetingId, CancellationToken ct = default);
    Task<MeetingRecordingResponse?> GetRecordingAsync(string recordingId, CancellationToken ct = default);
}
