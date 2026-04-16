using AFH.Acs.Recorder.Infrastructure.Data.Entities;

namespace AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;

public interface IRecordingRepository
{
    Task AddAsync(MeetingRecordingEntity entity, CancellationToken ct = default);
    Task<MeetingRecordingEntity?> GetByRecordingIdAsync(string recordingId, CancellationToken ct = default);
    Task UpdateAsync(MeetingRecordingEntity entity, CancellationToken ct = default);

    Task<MeetingRecordingEntity?> GetActiveByGroupIdAsync(string groupId, CancellationToken ct = default);

    Task<IReadOnlyList<MeetingRecordingEntity>> ListByMeetingIdAsync(string? meetingId, CancellationToken ct = default);
   

    Task<MeetingRecordingEntity?> GetRecordingWithMeetingAndClientAsync(
string recordingId,
CancellationToken ct = default);


    Task UpdateBlobInfoAsync(string recordingId, string blobName, string blobUrl,
        DateTime recordingStartUtc, DateTime recordingEndUtc, int durationSeconds, CancellationToken ct = default);
}