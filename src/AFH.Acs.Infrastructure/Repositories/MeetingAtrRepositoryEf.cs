using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace AFH.Acs.Recorder.Infrastructure.Data.Repositories;

public sealed class MeetingAtrRepositoryEf : IMeetingAtrRepository
{
    private readonly MeetingDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<MeetingAtrRepositoryEf> _logger;

    public MeetingAtrRepositoryEf(
        MeetingDbContext db,
        IMapper mapper,
        ILogger<MeetingAtrRepositoryEf> logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    // CREATE
    public async Task InsertAsync(MeetingAtrAnalysisEntity entity, CancellationToken ct = default)
    {
        entity.CreatedAtUtc = DateTime.UtcNow;

        await _db.MeetingAtrAnalyses.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    // READ
    public async Task<MeetingAtrAnalysisDto?> GetAnalysisAsync(
        string meetingId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId)) return null;

        return await _db.MeetingAtrAnalyses
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .ProjectTo<MeetingAtrAnalysisDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    // UPDATE
    public async Task UpdateAsync(
        MeetingAtrAnalysisEntity entity,
        CancellationToken ct = default)
    {
        entity.CreatedAtUtc = entity.CreatedAtUtc == default
            ? DateTime.UtcNow
            : entity.CreatedAtUtc;

        _db.MeetingAtrAnalyses.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    // DELETE (hard delete)
    public async Task<bool> DeleteAsync(
        string meetingId,
        CancellationToken ct = default)
    {
        var entity = await _db.MeetingAtrAnalyses
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId, ct);

        if (entity == null)
            return false;

        _db.MeetingAtrAnalyses.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // EXISTS
    public async Task<bool> ExistsAsync(
        string meetingId,
        CancellationToken ct = default)
    {
        return await _db.MeetingAtrAnalyses
            .AnyAsync(x => x.MeetingId == meetingId, ct);
    }
}