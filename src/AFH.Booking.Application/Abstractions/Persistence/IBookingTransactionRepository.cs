using AFH.Booking.Domain.Transactions;

namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IBookingTransactionRepository
{
    Task AddAsync(BookingTransaction transaction, CancellationToken ct);

    Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct);

    Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct);

    Task UpdateAsync(BookingTransaction transaction, CancellationToken ct);
    Task<BookingTransaction?> GetForUpdateAsync(
    string transactionId,
    CancellationToken ct);

    Task<BookingTransaction?> GetLatestByTransactionRefAsync(string transactionRef, CancellationToken ct)
        => Task.FromResult<BookingTransaction?>(null);
}
