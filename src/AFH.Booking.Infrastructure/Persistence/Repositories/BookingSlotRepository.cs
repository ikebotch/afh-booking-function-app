using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Infrastructure.Persistence.Mapping;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class BookingSlotRepository : IBookingSlotRepository
{
    private readonly BookingDbContext _db;

    public BookingSlotRepository(BookingDbContext db)
        => _db = db;

    public async Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct)
    {
        var models = slots.Select(s => s.ToModel()).ToList();
        _db.BookingSlots.AddRange(models);
        await Task.CompletedTask;
    }

    public async Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct)
    {
        var m = await _db.BookingSlots
            .Include(x => x.Hold)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == slotId, ct);

        //return m?.ToDomain(includeHold: true);
        return m?.ToDomain();
    }

    public async Task AddAsync(BookingSlot slot, CancellationToken ct)
       => await _db.BookingSlots.AddAsync(slot.ToModel(), ct);

    public async Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct)
    {
        var models = await _db.BookingSlots
            .Where(x => x.TransactionId == transactionId)
            .Include(x => x.Hold)
            .AsNoTracking()
            .ToListAsync(ct);

        return models.Select(m => m.ToDomain()).ToList();
        //return models.Select(m => m.ToDomain(includeHold: true)).ToList();
    }
}