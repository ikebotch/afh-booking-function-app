using AFH.Acs.Application.Models;
using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions.Meetings;

public interface IMeetingSessionService
{
    Task<MeetingSessionScheduleResult> ScheduleAsync(ScheduleMeetingCommand command, CancellationToken ct = default);
    Task<MeetingSession?> GetByIdAsync(string meetingId, CancellationToken ct = default);
    Task<MeetingSession?> GetByGroupIdAsync(string groupId, CancellationToken ct = default);
    Task<MeetingSession> RecordConsentAsync(RecordMeetingConsentCommand command, CancellationToken ct = default);
    Task<IssuedJoinToken> IssueJoinTokenAsync(IssueJoinTokenCommand command, CancellationToken ct = default);
}
