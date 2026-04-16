using AFH.Acs.Application.Abstractions.Advisers;
using AFH.Acs.Application.Abstractions.Identity;
using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Models;
using AFH.Acs.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Application.Services.Meetings;

public sealed class MeetingSessionService(
    IMeetingSessionRepository repository,
    IJoinTokenIssuer joinTokenIssuer,
    IAdviserInfoProvider adviserInfoProvider,
    ILogger<MeetingSessionService> logger,
    string joinBaseUrl) : IMeetingSessionService
{
    private const string PendingCalendarEventReference = "PENDING_CALENDAR_EVENT";

    public async Task<MeetingSessionScheduleResult> ScheduleAsync(ScheduleMeetingCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        AdviserInfo? adviser = null;
        try
        {
            adviser = await adviserInfoProvider.GetByIdAsync(command.AdviserId.Trim(), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve adviser info for AdviserId={AdviserId}. Continuing without adviser name.", command.AdviserId);
        }

        var groupId = Guid.NewGuid().ToString("N");
        var meetingId = Guid.NewGuid().ToString("N");
        var normalizedJoinBaseUrl = string.IsNullOrWhiteSpace(joinBaseUrl) ? "http://localhost:5173" : joinBaseUrl.TrimEnd('/');

        var session = new MeetingSession
        {
            MeetingId = meetingId,
            GroupId = groupId,
            AdviserId = command.AdviserId.Trim(),
            AdviserName = adviser?.DisplayName,
            LeadId = command.LeadId.Trim(),
            MeetingType = command.MeetingType.Trim(),
            Title = command.Title.Trim(),
            StartUtc = command.Start.ToUniversalTime(),
            EndUtc = command.End.ToUniversalTime(),
            ClientEmail = command.ClientEmail.Trim(),
            ClientName = string.IsNullOrWhiteSpace(command.ClientName) ? null : command.ClientName.Trim(),
            CalendarEventReference = PendingCalendarEventReference
        };

        session.EnsureScheduleWindowIsValid();

        logger.LogInformation(
            "Scheduling ACS session {MeetingId} for AdviserId={AdviserId} LeadId={LeadId} GroupId={GroupId}",
            meetingId,
            session.AdviserId,
            session.LeadId,
            groupId);

        await repository.InsertAsync(session, ct);

        return new MeetingSessionScheduleResult
        {
            MeetingId = meetingId,
            GroupId = groupId,
            CalendarEventReference = PendingCalendarEventReference,
            JoinCode = groupId,
            ClientJoinUrl = $"{normalizedJoinBaseUrl}/meeting/{groupId}?role=client",
            AdviserJoinUrl = $"{normalizedJoinBaseUrl}/meeting/{groupId}?role=adviser"
        };
    }

    public Task<MeetingSession?> GetByIdAsync(string meetingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
            throw new ArgumentException("meetingId is required.", nameof(meetingId));

        return repository.GetByIdAsync(meetingId.Trim(), ct);
    }

    public Task<MeetingSession?> GetByGroupIdAsync(string groupId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("groupId is required.", nameof(groupId));

        return repository.GetByGroupIdAsync(groupId.Trim(), ct);
    }

    public async Task<MeetingSession> RecordConsentAsync(RecordMeetingConsentCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.GroupId))
            throw new ArgumentException("GroupId is required.", nameof(command));

        var updated = await repository.UpdateConsentAsync(command.GroupId.Trim(), command.Consent, DateTimeOffset.UtcNow, ct);
        if (updated is null)
            throw new InvalidOperationException($"Meeting not found for GroupId={command.GroupId}.");

        return updated;
    }

    public async Task<IssuedJoinToken> IssueJoinTokenAsync(IssueJoinTokenCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.GroupId))
            throw new ArgumentException("GroupId is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.DisplayName))
            throw new ArgumentException("DisplayName is required.", nameof(command));

        var session = await repository.GetByGroupIdAsync(command.GroupId.Trim(), ct);
        if (session is null)
        {
            logger.LogWarning("Join token requested for missing GroupId={GroupId}", command.GroupId);
            throw new InvalidOperationException("Meeting not found for groupId.");
        }

        return await joinTokenIssuer.IssueForMeetingAsync(session, command.DisplayName.Trim(), command.Role.Trim(), ct);
    }
}
