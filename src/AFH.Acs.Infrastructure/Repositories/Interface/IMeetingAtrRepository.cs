using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;

namespace AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;

public interface IMeetingAtrRepository
{
    Task<MeetingAtrAnalysisDto?> GetAnalysisAsync(string meetingId, CancellationToken ct = default);

    Task InsertAsync(MeetingAtrAnalysisEntity entity, CancellationToken ct = default);

    Task UpdateAsync(MeetingAtrAnalysisEntity entity, CancellationToken ct = default);

    Task<bool> DeleteAsync(string meetingId, CancellationToken ct = default);

    Task<bool> ExistsAsync(string meetingId, CancellationToken ct = default);
}