namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly BookingDbContext _db;

    public BookingRepository(BookingDbContext db)
    {
        _db = db;
    }

    public Task<BookingsModel?> GetAsync(BookingId id, CancellationToken ct)
        => _db.Bookings.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task SaveAsync(BookingsModel booking, CancellationToken ct)
    {
        var exists = await _db.Bookings.AnyAsync(x => x.Id == booking.Id, ct);
        if (!exists)
            _db.Bookings.Add(booking);
        else
            _db.Bookings.Update(booking);

        await _db.SaveChangesAsync(ct);
    }

    // New methods from the interface

    public Task<IReadOnlyList<BookingsModel>> GetScheduleAsync(
        string adviserId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        return _db.Bookings
            .Where(b => b.AdviserId == adviserId &&
                        b.StartUtc >= startUtc &&
                        b.EndUtc <= endUtc)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<BookingsModel>)t.Result, ct);
    }

    public Task<IReadOnlyList<BookingsModel>> GetByCustomerAsync(
        string customerId, CancellationToken ct)
    {
        return _db.Bookings
            .Where(b => b.CustomerId == customerId)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<BookingsModel>)t.Result, ct);
    }

    public Task<IReadOnlyList<BookingsModel>> GetByAdviserAsync(
        string adviserId, CancellationToken ct)
    {
        return _db.Bookings
            .Where(b => b.AdviserId == adviserId)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<BookingsModel>)t.Result, ct);
    }
}
