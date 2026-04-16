using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;

namespace AFH.Acs.Function.Services.Recordings;

public sealed class LiveAcsMeetingRecordingService : IMeetingRecordingService
{
    private static NotSupportedException NotImplemented()
        => new("Live ACS call recording is not implemented yet. Configure Recording:Mode=Metadata until the live integration is added.");

    public Task<MeetingRecordingResponse> StartRecordingAsync(StartRecordingRequest request, CancellationToken ct = default)
        => Task.FromException<MeetingRecordingResponse>(NotImplemented());

    public Task<MeetingRecordingResponse> StopRecordingAsync(StopRecordingRequest request, CancellationToken ct = default)
        => Task.FromException<MeetingRecordingResponse>(NotImplemented());

    public Task<IReadOnlyList<MeetingRecordingResponse>> ListRecordingsAsync(string? meetingId, CancellationToken ct = default)
        => Task.FromException<IReadOnlyList<MeetingRecordingResponse>>(NotImplemented());

    public Task<MeetingRecordingResponse?> GetRecordingAsync(string recordingId, CancellationToken ct = default)
        => Task.FromException<MeetingRecordingResponse?>(NotImplemented());
}
