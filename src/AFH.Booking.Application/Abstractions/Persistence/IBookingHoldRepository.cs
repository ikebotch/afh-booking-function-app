namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IBookingHoldRepository
{
    Task AddAsync(BookingHold hold, CancellationToken ct);

    Task<BookingHold?> GetAsync(string holdId, CancellationToken ct);

    Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct);

    Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct);
    Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct);
    Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct);

    Task UpdateAsync(BookingHold hold, CancellationToken ct);


    Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct);

    Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct);
}
