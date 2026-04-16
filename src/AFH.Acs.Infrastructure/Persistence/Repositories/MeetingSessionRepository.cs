using AFH.Acs.Application.Abstractions;
using AFH.Acs.Domain.Entities;
using AFH.Acs.Domain.Enums;
using AFH.Acs.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Acs.Infrastructure.Persistence.Repositories;

public sealed class MeetingSessionRepository(MeetingDbContext dbContext) : IMeetingSessionRepository
{
    public async Task InsertAsync(MeetingSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await dbContext.Meetings.AddAsync(ToEntity(session), ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<MeetingSession?> GetByIdAsync(string meetingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
            return null;

        var entity = await BaseQuery()
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId, ct);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<MeetingSession?> GetByGroupIdAsync(string groupId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return null;

        var entity = await BaseQuery()
            .FirstOrDefaultAsync(x => x.GroupId == groupId, ct);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<MeetingSession?> UpdateConsentAsync(string groupId, bool consent, DateTimeOffset consentTimestampUtc, CancellationToken ct = default)
    {
        var entity = await dbContext.Meetings.FirstOrDefaultAsync(x => x.GroupId == groupId, ct);
        if (entity is null)
            return null;

        entity.ConsentToRecording = consent;
        entity.ConsentTimestampUtc = consent ? consentTimestampUtc.UtcDateTime : null;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(entity.MeetingId, ct);
    }

    private IQueryable<MeetingEntity> BaseQuery()
        => dbContext.Meetings
            .AsNoTracking()
            .Include(x => x.Adviser)
            .Include(x => x.Lead)
            .Include(x => x.Attendees)
            .Include(x => x.Recordings)
            .Include(x => x.Transcription);

    private static MeetingEntity ToEntity(MeetingSession session)
        => new()
        {
            MeetingId = session.MeetingId,
            GroupId = session.GroupId,
            AdviserId = session.AdviserId,
            LeadId = session.LeadId,
            MeetingType = session.MeetingType,
            Title = session.Title,
            StartUtc = session.StartUtc.UtcDateTime,
            EndUtc = session.EndUtc.UtcDateTime,
            ClientEmail = session.ClientEmail,
            ConsentToRecording = session.ConsentToRecording,
            ConsentTimestampUtc = session.ConsentTimestampUtc?.UtcDateTime,
            Status = session.Status.ToString().ToUpperInvariant(),
            CreatedAtUtc = DateTime.UtcNow,
            GraphEventId = session.CalendarEventReference
        };

    private static MeetingSession ToDomain(MeetingEntity entity)
        => new()
        {
            MeetingId = entity.MeetingId,
            GroupId = entity.GroupId,
            AdviserId = entity.AdviserId,
            AdviserName = entity.Adviser?.FullName,
            LeadId = entity.LeadId,
            MeetingType = entity.MeetingType,
            Title = entity.Title,
            StartUtc = new DateTimeOffset(DateTime.SpecifyKind(entity.StartUtc, DateTimeKind.Utc)),
            EndUtc = new DateTimeOffset(DateTime.SpecifyKind(entity.EndUtc, DateTimeKind.Utc)),
            ClientEmail = entity.ClientEmail,
            ClientName = entity.Lead?.ClientName,
            ConsentToRecording = entity.ConsentToRecording,
            ConsentTimestampUtc = entity.ConsentTimestampUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.ConsentTimestampUtc.Value, DateTimeKind.Utc))
                : null,
            CalendarEventReference = entity.GraphEventId,
            Status = Enum.TryParse<MeetingSessionStatus>(entity.Status, ignoreCase: true, out var parsedStatus)
                ? parsedStatus
                : MeetingSessionStatus.Scheduled,
            Attendees = entity.Attendees.Select(x => new MeetingAttendee
            {
                Email = x.Email,
                Role = x.Role,
                ResponseStatus = x.ResponseStatus,
                ResponseTimeUtc = x.ResponseTimeUtc.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(x.ResponseTimeUtc.Value, DateTimeKind.Utc))
                    : null
            }).ToArray(),
            Recordings = entity.Recordings.Select(x => new MeetingRecordingArtifact
            {
                RecordingId = x.RecordingId,
                BlobName = x.BlobName,
                BlobUrl = x.BlobUrl,
                RecordingStartUtc = new DateTimeOffset(DateTime.SpecifyKind(x.RecordingStartUtc, DateTimeKind.Utc)),
                RecordingEndUtc = x.RecordingEndUtc.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(x.RecordingEndUtc.Value, DateTimeKind.Utc))
                    : null,
                DurationSeconds = x.DurationSeconds
            }).ToArray(),
            Transcription = entity.Transcription is null
                ? null
                : new MeetingTranscriptionArtifact
                {
                    TranscriptionId = entity.Transcription.TranscriptionId,
                    Language = entity.Transcription.Language,
                    FullText = entity.Transcription.FullText,
                    SummaryText = entity.Transcription.SummaryText
                }
        };
}
