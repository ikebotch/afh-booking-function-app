using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions.Meetings;

public interface IMeetingSessionRepository
{
    Task InsertAsync(MeetingSession session, CancellationToken ct = default);
    Task<MeetingSession?> GetByIdAsync(string meetingId, CancellationToken ct = default);
    Task<MeetingSession?> GetByGroupIdAsync(string groupId, CancellationToken ct = default);
    Task<MeetingSession?> UpdateConsentAsync(string groupId, bool consent, DateTimeOffset consentTimestampUtc, CancellationToken ct = default);
}
