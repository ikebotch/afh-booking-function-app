using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions.Recordings;

public interface IMeetingRecordingRepository
{
    Task<MeetingRecordingArtifact> StartAsync(string meetingId, string blobName, string blobUrl, DateTimeOffset startedUtc, CancellationToken ct = default);
    Task<MeetingRecordingArtifact?> StopAsync(string recordingId, DateTimeOffset stoppedUtc, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingRecordingArtifact>> ListAsync(string? meetingId, CancellationToken ct = default);
    Task<MeetingRecordingArtifact?> GetAsync(string recordingId, CancellationToken ct = default);
}
