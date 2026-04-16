
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AFH.Acs.Recorder.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Recorder.Infrastructure.Data.Repositories;

public class RecordingRepositoryEf : IRecordingRepository
{
    private readonly MeetingDbContext _db;
    private readonly ILogger<RecordingService> _logger;
    public RecordingRepositoryEf(MeetingDbContext db, ILogger<RecordingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddAsync(MeetingRecordingEntity entity, CancellationToken ct = default)
    {
        _db.MeetingRecordings.Add(entity);
        await _db.SaveChangesAsync(ct);
    }


    public async Task<IReadOnlyList<MeetingRecordingEntity>> ListByMeetingIdAsync(
        string? meetingId,
        CancellationToken ct = default)
    {
        IQueryable<MeetingRecordingEntity> query = _db.MeetingRecordings.AsNoTracking().Include(r => r.Meeting)
                .ThenInclude(m => m.Lead)
            .Include(r => r.Meeting)
                .ThenInclude(m => m.Adviser);

        if (!string.IsNullOrWhiteSpace(meetingId))
        {
            query = query.Where(x => x.MeetingId == meetingId);
        }

        return await query
            .OrderByDescending(x => x.RecordingStartUtc)
            .ToListAsync(ct);
    }

    public Task<MeetingRecordingEntity?> GetByRecordingIdAsync(
        string recordingId,
        CancellationToken ct = default)
    {
        return _db.MeetingRecordings
                  .AsNoTracking()
                  .FirstOrDefaultAsync(x => x.RecordingId == recordingId, ct);
    }


    public async Task<MeetingRecordingEntity?> GetRecordingWithMeetingAndClientAsync(
string recordingId,
CancellationToken ct = default)
    {
        return await _db.MeetingRecordings
            .Include(r => r.Meeting)
                .ThenInclude(m => m.Lead)
            .Include(r => r.Meeting)
                .ThenInclude(m => m.Adviser)
            .FirstOrDefaultAsync(r => r.RecordingId == recordingId, ct);
    }



    public async Task UpdateAsync(MeetingRecordingEntity entity, CancellationToken ct = default)
    {
        _db.MeetingRecordings.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MeetingRecordingEntity?> GetActiveByGroupIdAsync(string groupId, CancellationToken ct = default)
    {
        return await _db.MeetingRecordings
            .Where(r => r.GroupId == groupId && r.DurationSeconds == null)
            .OrderByDescending(r => r.RecordingStartUtc)
            .FirstOrDefaultAsync(ct);
    }


    public async Task UpdateBlobInfoAsync(
        string recordingId,
        string blobName,
        string blobUrl,
        DateTime recordingStartUtc,
        DateTime recordingEndUtc,
        int durationSeconds,
        CancellationToken ct = default)
    {
        var entity = await _db.MeetingRecordings
            .FirstOrDefaultAsync(x => x.RecordingId == recordingId, ct);

        if (entity == null)
        {
            _logger.LogWarning(
                "UpdateBlobInfoAsync: no MeetingRecordingEntity found for RecordingId={RecordingId}",
                recordingId);
            return;
        }

        entity.BlobName = blobName;
        entity.BlobUrl = blobUrl;
        entity.RecordingStartUtc = recordingStartUtc;
        entity.RecordingEndUtc = recordingEndUtc;
        entity.DurationSeconds = durationSeconds;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
          "Updated blob info for RecordingId={RecordingId}. BlobName={BlobName}, BlobUrl={BlobUrl}",
          recordingId,
          entity.BlobName,
          entity.BlobUrl);
    }


}