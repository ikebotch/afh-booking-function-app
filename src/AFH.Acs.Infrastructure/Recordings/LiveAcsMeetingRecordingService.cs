using AFH.Acs.Application.Abstractions.Recordings;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;

namespace AFH.Acs.Infrastructure.Recordings;

public sealed class LiveAcsMeetingRecordingService : IMeetingRecordingService
{
    public Task<MeetingRecordingResponse> StartRecordingAsync(StartRecordingRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("Live ACS recording is not enabled in this environment.");

    public Task<MeetingRecordingResponse> StopRecordingAsync(StopRecordingRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("Live ACS recording is not enabled in this environment.");

    public Task<IReadOnlyList<MeetingRecordingResponse>> ListRecordingsAsync(string? meetingId, CancellationToken ct = default)
        => throw new NotSupportedException("Live ACS recording is not enabled in this environment.");

    public Task<MeetingRecordingResponse?> GetRecordingAsync(string recordingId, CancellationToken ct = default)
        => throw new NotSupportedException("Live ACS recording is not enabled in this environment.");
}
