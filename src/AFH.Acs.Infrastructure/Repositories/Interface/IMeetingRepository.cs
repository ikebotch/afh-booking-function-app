using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;

namespace AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;

public interface IMeetingRepository
{
    Task InsertAsync(MeetingEntity entity, CancellationToken ct = default);

    Task<MeetingEntity?> GetByIdAsync(string meetingId, CancellationToken ct = default);

    Task<MeetingDetailsDto?> GetByGroupIdAsync(string groupId, CancellationToken ct = default);

    Task<MeetingEntity?> UpdateConsentByGroupIdAsync(
        string groupId,
        bool consent,
        DateTimeOffset consentTimestampUtc,
        CancellationToken ct = default);
}