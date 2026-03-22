using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class AdviserAvailabilityProjectionRepository : IAdviserAvailabilityProjectionRepository
{
    private readonly BookingDbContext _db;

    public AdviserAvailabilityProjectionRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task UpsertBusyBlockAsync(AdviserBusyBlockProjection block, CancellationToken ct)
    {
        var existing = await _db.AdviserAvailabilityBlocks
            .FirstOrDefaultAsync(
                x => x.AdviserId == block.AdviserId && x.ProviderEventId == block.ProviderEventId,
                ct);

        if (existing is null)
        {
            await _db.AdviserAvailabilityBlocks.AddAsync(new AdviserAvailabilityBlockModel
            {
                Id = string.IsNullOrWhiteSpace(block.Id) ? Guid.NewGuid().ToString("N") : block.Id,
                AdviserId = block.AdviserId,
                ProviderEventId = block.ProviderEventId,
                CalendarId = block.CalendarId,
                Subject = block.Subject,
                StartUtc = block.StartUtc,
                EndUtc = block.EndUtc,
                IsCancelled = block.IsCancelled,
                ChangeKey = block.ChangeKey,
                ICalUId = block.ICalUId,
                LastSyncedUtc = block.LastSyncedUtc,
                SourceReceiptId = block.SourceReceiptId
            }, ct);
            return;
        }

        existing.CalendarId = block.CalendarId;
        existing.Subject = block.Subject;
        existing.StartUtc = block.StartUtc;
        existing.EndUtc = block.EndUtc;
        existing.IsCancelled = block.IsCancelled;
        existing.ChangeKey = block.ChangeKey;
        existing.ICalUId = block.ICalUId;
        existing.LastSyncedUtc = block.LastSyncedUtc;
        existing.SourceReceiptId = block.SourceReceiptId;
    }

    public async Task DeleteBusyBlockAsync(string adviserId, string providerEventId, DateTime syncedUtc, CancellationToken ct)
    {
        var existing = await _db.AdviserAvailabilityBlocks
            .FirstOrDefaultAsync(
                x => x.AdviserId == adviserId && x.ProviderEventId == providerEventId,
                ct);

        if (existing is null)
            return;

        existing.IsCancelled = true;
        existing.LastSyncedUtc = syncedUtc;
    }

    public async Task<IReadOnlyList<AdviserBusyBlockProjection>> ListBusyBlocksAsync(
        string adviserId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct)
    {
        var rows = await _db.AdviserAvailabilityBlocks
            .AsNoTracking()
            .Where(x => x.AdviserId == adviserId)
            .Where(x => !x.IsCancelled)
            .Where(x => x.EndUtc > startUtc && x.StartUtc < endUtc)
            .OrderBy(x => x.StartUtc)
            .ToListAsync(ct);

        return rows
            .Select(x => new AdviserBusyBlockProjection
            {
                Id = x.Id,
                AdviserId = x.AdviserId,
                ProviderEventId = x.ProviderEventId,
                CalendarId = x.CalendarId,
                Subject = x.Subject,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                IsCancelled = x.IsCancelled,
                ChangeKey = x.ChangeKey,
                ICalUId = x.ICalUId,
                LastSyncedUtc = x.LastSyncedUtc,
                SourceReceiptId = x.SourceReceiptId
            })
            .ToList();
    }

    public async Task<DateTime?> GetLastSyncedUtcAsync(string adviserId, CancellationToken ct)
    {
        return await _db.AdviserAvailabilityBlocks
            .AsNoTracking()
            .Where(x => x.AdviserId == adviserId)
            .Select(x => (DateTime?)x.LastSyncedUtc)
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync(ct);
    }
}
