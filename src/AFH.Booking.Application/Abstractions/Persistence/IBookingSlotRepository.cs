namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IBookingSlotRepository
{
    Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct);

    Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct);

    Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct);

    Task AddAsync(BookingSlot slot, CancellationToken ct);

    Task UpdateAsync(BookingSlot slot, CancellationToken ct);
}
