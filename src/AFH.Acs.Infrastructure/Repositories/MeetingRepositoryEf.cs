using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Recorder.Infrastructure.Data.Repositories;

public class MeetingRepositoryEf : IMeetingRepository
{
    private readonly MeetingDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<MeetingRepositoryEf> _logger;

    public MeetingRepositoryEf(MeetingDbContext db, IMapper mapper, ILogger<MeetingRepositoryEf> logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task InsertAsync(MeetingEntity entity, CancellationToken ct = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));


        try
        {
            await _db.Meetings.AddAsync(entity, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx)
        {
            _logger.LogError(
                sqlEx,
                "SQL error inserting MeetingEntity. Number={Number}, State={State}, Message={Message}, Entity={@Entity}",
                sqlEx.Number,
                sqlEx.State,
                sqlEx.Message,
                entity);

            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "DbUpdateException inserting MeetingEntity. Inner={InnerMessage}, Entity={@Entity}",
                ex.InnerException?.Message,
                entity);

            throw;
        }

    }


    public async Task<MeetingEntity?> GetByIdAsync(
        string meetingId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
            return null;

        return await BaseQuery()
            .Include(m => m.Attendees)
            .Include(m => m.Recordings)
            .Include(m => m.Transcription)
            .Where(m => m.MeetingId == meetingId)
            //.ProjectTo<MeetingDetailsDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }



    public async Task<MeetingDetailsDto?> GetByGroupIdAsync(
        string groupId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return null;

        return await BaseQuery()
            .Where(m => m.GroupId == groupId)
            .ProjectTo<MeetingDetailsDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }


    public async Task UpdateAsync(
    string recordingId,
    Action<MeetingRecordingEntity> updateAction,
    CancellationToken ct = default)
    {
        var entity = await _db.MeetingRecordings
            .FirstOrDefaultAsync(x => x.RecordingId == recordingId, ct);

        if (entity == null)
        {
            _logger.LogWarning("No MeetingRecordingEntity found for RecordingId={RecordingId}", recordingId);
            return;
        }

        // Apply whatever changes the caller wants
        updateAction(entity);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated MeetingRecordingEntity for RecordingId={RecordingId}", recordingId);
    }


    public async Task<MeetingEntity?> UpdateConsentByGroupIdAsync(
        string groupId,
        bool consent,
        DateTimeOffset consentTimestampUtc,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("groupId is required.", nameof(groupId));

        var entity = await _db.Meetings
            .SingleOrDefaultAsync(m => m.GroupId == groupId, ct);

        if (entity == null)
        {
            return null;
        }

        entity.ConsentToRecording = consent;
        entity.ConsentTimestampUtc = consent ? consentTimestampUtc.UtcDateTime : (DateTime?)null;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return entity;
    }

    private IQueryable<MeetingEntity> BaseQuery()
    {
        return _db.Meetings
            .AsNoTracking()
            .Include(m => m.Adviser)
            .Include(m => m.Lead)
            .Include(m => m.Attendees)
            .Include(m => m.Recordings)
            .Include(m => m.Transcription);
    }
}