using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Infrastructure.Persistence.Mapping;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class BookingHoldRepository : IBookingHoldRepository
{
    private readonly BookingDbContext _db;

    public BookingHoldRepository(BookingDbContext db)
        => _db = db;

    public async Task AddAsync(BookingHold hold, CancellationToken ct)
    {
        if (hold is null) throw new ArgumentNullException(nameof(hold));

        var model = hold.ToModel();
        await _db.Holds.AddAsync(model, ct);
    }

   
    public async Task<BookingHold?> GetAsync(string holdId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(holdId))
            throw new ArgumentException("holdId is required.", nameof(holdId));

        var m = await _db.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == holdId, ct);

        return m is null ? null : m.ToDomain();
    }

    public async Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(holdId))
            throw new ArgumentException("holdId is required.", nameof(holdId));

        // tracked entity
        var m = await _db.Holds
            .FirstOrDefaultAsync(x => x.Id == holdId, ct);

        return m is null ? null : m.ToDomain();
    }

    public async Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct)
    {
        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        if (take <= 0) take = 100;

        var models = await _db.Holds
            .AsNoTracking()
            .Where(x => x.Status == HoldStatus.Active)
            .Where(x => x.HoldExpiresUtc <= utcNow)
            .OrderBy(x => x.HoldExpiresUtc)
            .Take(take)
            .ToListAsync(ct);

        return models.Select(m => m.ToDomain()).ToList();
    }


    public async Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(holdId))
            throw new ArgumentException("holdId is required.", nameof(holdId));

        var m = await _db.Holds
            .FirstOrDefaultAsync(x => x.Id == holdId, ct);

        return m is null ? null : m.ToDomain();
    }

    public async Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            throw new ArgumentException("slotId is required.", nameof(slotId));

        var m = await _db.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SlotId == slotId, ct);

        return m is null ? null : m.ToDomain();
    }

    public async Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerEventId))
            throw new ArgumentException("providerEventId is required.", nameof(providerEventId));

        var m = await _db.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CalendarProviderEventId == providerEventId, ct);

        return m is null ? null : m.ToDomain();
    }

    public async Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            throw new ArgumentException("slotId is required.", nameof(slotId));

        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        var m = await _db.Holds
            .AsNoTracking()
            .Where(x => x.SlotId == slotId)
            .Where(x => x.HoldExpiresUtc > utcNow)
            .Where(x => x.Status == (int)BookingHoldStatus.Active)
            .FirstOrDefaultAsync(ct);

        return m is null ? null : m.ToDomain();
    }

    public async Task<BookingHold?> GetActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentException("transactionId is required.", nameof(transactionId));

        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        var model = await _db.Holds
            .AsNoTracking()
            .Include(x => x.Slot)
            .Where(x => x.Slot.TransactionId == transactionId)
            .Where(x => x.HoldExpiresUtc > utcNow)
            .Where(x => x.Status == HoldStatus.Active)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        return model is null ? null : model.ToDomain();
    }

    public async Task UpdateAsync(BookingHold hold, CancellationToken ct)
    {
        if (hold is null) throw new ArgumentNullException(nameof(hold));

        // Prefer: attach & set current values (works even if not tracked yet)
        var existing = await _db.Holds
            .FirstOrDefaultAsync(x => x.Id == hold.Id, ct);

        if (existing is null)
            throw new InvalidOperationException($"Hold '{hold.Id}' not found.");

        // map domain -> existing tracked model
        hold.ApplyToModel(existing);
    }
}
