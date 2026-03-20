using AFH.Booking.Application.Abstractions.Persistence;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BookingDbContext _db;

    public UnitOfWork(BookingDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);
}
