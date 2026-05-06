using AFH.Booking.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class MeetingTypeRepository : IMeetingTypeRepository
{
    private readonly BookingDbContext _db;

    public MeetingTypeRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MeetingTypeRecord>> ListActiveAsync(CancellationToken ct)
    {
        var rows = await _db.MeetingTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync(ct);

        return rows.Select(x => new MeetingTypeRecord
        {
            Code = x.Code,
            Label = x.Label,
            IsDefault = x.IsDefault,
            DefaultDurationMinutes = x.DefaultDurationMinutes,
            SortOrder = x.SortOrder
        }).ToList();
    }

    public async Task<MeetingTypeRecord> UpsertAsync(MeetingTypeUpsert change, CancellationToken ct)
    {
        var code = change.Code.Trim();
        var label = string.IsNullOrWhiteSpace(change.Label) ? code : change.Label.Trim();
        var existing = await _db.MeetingTypes.FirstOrDefaultAsync(x => x.Code == code, ct);

        if (existing is null)
        {
            existing = new()
            {
                Code = code,
                CreatedUtc = change.ChangedUtc
            };
            await _db.MeetingTypes.AddAsync(existing, ct);
        }

        existing.Label = label;
        existing.IsDefault = change.IsDefault;
        existing.IsActive = change.IsActive;
        existing.DefaultDurationMinutes = change.DefaultDurationMinutes is > 0 ? change.DefaultDurationMinutes : null;
        existing.SortOrder = change.SortOrder;
        existing.UpdatedUtc = change.ChangedUtc;

        return new MeetingTypeRecord
        {
            Code = existing.Code,
            Label = existing.Label,
            IsDefault = existing.IsDefault,
            DefaultDurationMinutes = existing.DefaultDurationMinutes,
            SortOrder = existing.SortOrder
        };
    }

    public async Task<bool> DeactivateAsync(string code, DateTime changedUtc, CancellationToken ct)
    {
        var normalized = code.Trim();
        var existing = await _db.MeetingTypes.FirstOrDefaultAsync(x => x.Code == normalized, ct);
        if (existing is null)
            return false;

        existing.IsActive = false;
        existing.UpdatedUtc = changedUtc;
        return true;
    }
}
