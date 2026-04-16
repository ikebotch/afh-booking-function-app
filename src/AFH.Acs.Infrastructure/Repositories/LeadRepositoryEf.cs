

using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Acs.Recorder.Infrastructure.Repositories;

public sealed class LeadRepositoryEf : ILeadRepository
{
    private readonly MeetingDbContext _db;

    public LeadRepositoryEf(MeetingDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<LeadEntity> Items, int TotalCount)> ListAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 10;

        IQueryable<LeadEntity> q = _db.Leads; // adjust DbSet name if needed

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(l =>
                l.LeadId.Contains(term) ||
                l.ClientName.Contains(term) ||
                l.ClientEmail.Contains(term));
        }

        var total = await q.CountAsync(ct).ConfigureAwait(false);

        var items = await q
            .OrderBy(l => l.ClientName)
            .ThenBy(l => l.LeadId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, total);
    }

    public Task<LeadEntity?> GetByIdAsync(
        string leadId,
        CancellationToken ct = default)
    {
        return _db.Leads.FirstOrDefaultAsync(l => l.LeadId == leadId, ct);
    }
}