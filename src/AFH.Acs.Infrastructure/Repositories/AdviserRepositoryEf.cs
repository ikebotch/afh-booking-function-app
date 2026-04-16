using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Acs.Recorder.Infrastructure.Repositories;

public sealed class AdviserRepositoryEf : IAdviserRepository
{
    private readonly MeetingDbContext _db;

    public AdviserRepositoryEf(MeetingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdviserEntity>> ListAsync(
        string? region,
        bool leadTechOnly,
        CancellationToken ct = default)
    {
        IQueryable<AdviserEntity> query = _db.Advisers;

        if (!string.IsNullOrWhiteSpace(region))
        {
            query = query.Where(a => a.Region == region);
        }

        if (leadTechOnly)
        {
            query = query.Where(a => a.LeadTechFlag);
        }

        // only active advisers
        query = query.Where(a => a.ActiveFlag);

        return await query
            .OrderBy(a => a.Region)
            .ThenBy(a => a.FullName)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public Task<AdviserEntity?> GetByIdAsync(
        string adviserId,
        CancellationToken ct = default)
    {
        return _db.Advisers
            .FirstOrDefaultAsync(a => a.AdviserId == adviserId, ct);
    }
}
