using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Infrastructure.Persistence.Mapping;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class BookingTransactionRepository : IBookingTransactionRepository
{
    private readonly BookingDbContext _db;

    public BookingTransactionRepository(BookingDbContext db)
        => _db = db;

    public async Task AddAsync(BookingTransaction tx, CancellationToken ct)
    {
        _db.BookingTransactions.Add(tx.ToModel());
        await Task.CompletedTask;
    }

    public async Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct)
    {
        var m = await _db.BookingTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transactionId, ct);

        return m?.ToDomain();
    }

    public async Task<BookingTransaction?> GetForUpdateAsync(
    string transactionId,
    CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentException("transactionId is required.", nameof(transactionId));

        var model = await _db.BookingTransactions
            .Include(x => x.Slots)
                .ThenInclude(s => s.Hold)
            .SingleOrDefaultAsync(x => x.Id == transactionId, ct);

        return model?.ToDomain(includeSlots: true);
    }

    public async Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct)
    {
        var m = await _db.BookingTransactions
            .Include(x => x.Slots)
            .ThenInclude(s => s.Hold)
            .FirstOrDefaultAsync(x => x.Id == transactionId, ct);

        return m?.ToDomain(includeSlots: true);
    }

    public async Task<BookingTransaction?> GetLatestByTransactionRefAsync(string transactionRef, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionRef))
            return null;

        var m = await _db.BookingTransactions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(x => x.TransactionRef == transactionRef.Trim(), ct);

        return m?.ToDomain();
    }

    public async Task UpdateAsync(BookingTransaction tx, CancellationToken ct)
    {
        var model = await _db.BookingTransactions
            .FirstAsync(x => x.Id == tx.Id, ct);
        tx.ApplyToModel(model);
    }
}