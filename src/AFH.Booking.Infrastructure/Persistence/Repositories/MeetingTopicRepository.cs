using AFH.Booking.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class MeetingTopicRepository : IMeetingTopicRepository
{
    private readonly BookingDbContext _db;

    public MeetingTopicRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MeetingTopicRecord>> ListActiveAsync(CancellationToken ct)
    {
        var rows = await _db.MeetingTopics
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync(ct);

        return rows.Select(x => new MeetingTopicRecord
        {
            Code = x.Code,
            Label = x.Label,
            IsDefault = x.IsDefault,
            SortOrder = x.SortOrder
        }).ToList();
    }

    public async Task<MeetingTopicRecord> UpsertAsync(MeetingTopicUpsert change, CancellationToken ct)
    {
        var code = change.Code.Trim();
        var label = string.IsNullOrWhiteSpace(change.Label) ? code : change.Label.Trim();
        var existing = await _db.MeetingTopics.FirstOrDefaultAsync(x => x.Code == code, ct);

        if (existing is null)
        {
            existing = new()
            {
                Code = code,
                CreatedUtc = change.ChangedUtc
            };
            await _db.MeetingTopics.AddAsync(existing, ct);
        }

        existing.Label = label;
        existing.IsDefault = change.IsDefault;
        existing.IsActive = change.IsActive;
        existing.SortOrder = change.SortOrder;
        existing.UpdatedUtc = change.ChangedUtc;

        return new MeetingTopicRecord
        {
            Code = existing.Code,
            Label = existing.Label,
            IsDefault = existing.IsDefault,
            SortOrder = existing.SortOrder
        };
    }
}
