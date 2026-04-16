using AFH.Acs.Application.Abstractions.Recordings;
using AFH.Acs.Domain.Entities;
using AFH.Acs.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Acs.Infrastructure.Persistence.Repositories;

public sealed class MeetingRecordingRepository(MeetingDbContext dbContext) : IMeetingRecordingRepository
{
    public async Task<MeetingRecordingArtifact> StartAsync(string meetingId, string blobName, string blobUrl, DateTimeOffset startedUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
            throw new ArgumentException("meetingId is required.", nameof(meetingId));

        var meeting = await dbContext.Meetings.FirstOrDefaultAsync(x => x.MeetingId == meetingId, ct)
            ?? throw new InvalidOperationException($"Meeting not found for MeetingId={meetingId}.");

        var entity = new MeetingRecordingEntity
        {
            RecordingId = Guid.NewGuid().ToString("N"),
            MeetingId = meeting.MeetingId,
            BlobName = blobName,
            BlobUrl = blobUrl,
            RecordingStartUtc = startedUtc.UtcDateTime
        };

        await dbContext.MeetingRecordings.AddAsync(entity, ct);
        meeting.Recordings.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        return ToDomain(entity);
    }

    public async Task<MeetingRecordingArtifact?> StopAsync(string recordingId, DateTimeOffset stoppedUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recordingId))
            return null;

        var entity = await dbContext.MeetingRecordings.FirstOrDefaultAsync(x => x.RecordingId == recordingId, ct);
        if (entity is null)
            return null;

        entity.RecordingEndUtc = stoppedUtc.UtcDateTime;
        entity.DurationSeconds = (int)Math.Max(0, Math.Round((stoppedUtc.UtcDateTime - entity.RecordingStartUtc).TotalSeconds));
        await dbContext.SaveChangesAsync(ct);

        return ToDomain(entity);
    }

    public async Task<IReadOnlyList<MeetingRecordingArtifact>> ListAsync(string? meetingId, CancellationToken ct = default)
    {
        var query = dbContext.MeetingRecordings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(meetingId))
        {
            query = query.Where(x => x.MeetingId == meetingId);
        }

        var items = await query
            .OrderByDescending(x => x.RecordingStartUtc)
            .Select(x => new MeetingRecordingArtifact
            {
                RecordingId = x.RecordingId,
                MeetingId = x.MeetingId,
                BlobName = x.BlobName,
                BlobUrl = x.BlobUrl,
                RecordingStartUtc = new DateTimeOffset(DateTime.SpecifyKind(x.RecordingStartUtc, DateTimeKind.Utc)),
                RecordingEndUtc = x.RecordingEndUtc.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(x.RecordingEndUtc.Value, DateTimeKind.Utc))
                    : null,
                DurationSeconds = x.DurationSeconds
            })
            .ToArrayAsync(ct);

        return items;
    }

    public async Task<MeetingRecordingArtifact?> GetAsync(string recordingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recordingId))
            return null;

        var entity = await dbContext.MeetingRecordings.AsNoTracking().FirstOrDefaultAsync(x => x.RecordingId == recordingId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    private static MeetingRecordingArtifact ToDomain(MeetingRecordingEntity entity)
        => new()
        {
            RecordingId = entity.RecordingId,
            MeetingId = entity.MeetingId,
            BlobName = entity.BlobName,
            BlobUrl = entity.BlobUrl,
            RecordingStartUtc = new DateTimeOffset(DateTime.SpecifyKind(entity.RecordingStartUtc, DateTimeKind.Utc)),
            RecordingEndUtc = entity.RecordingEndUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.RecordingEndUtc.Value, DateTimeKind.Utc))
                : null,
            DurationSeconds = entity.DurationSeconds
        };
}
