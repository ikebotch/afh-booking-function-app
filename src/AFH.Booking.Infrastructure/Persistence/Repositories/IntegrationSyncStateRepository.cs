using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class IntegrationSyncStateRepository : IIntegrationSyncStateRepository
{
    private readonly BookingDbContext _db;

    public IntegrationSyncStateRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        var row = await _db.IntegrationSyncStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);

        return row?.Value;
    }

    public async Task UpsertValueAsync(string key, string value, DateTime updatedUtc, CancellationToken ct)
    {
        var row = await _db.IntegrationSyncStates.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (row is null)
        {
            await _db.IntegrationSyncStates.AddAsync(new IntegrationSyncStateModel
            {
                Key = key,
                Value = value,
                UpdatedUtc = updatedUtc
            }, ct);
            return;
        }

        row.Value = value;
        row.UpdatedUtc = updatedUtc;
    }
}
