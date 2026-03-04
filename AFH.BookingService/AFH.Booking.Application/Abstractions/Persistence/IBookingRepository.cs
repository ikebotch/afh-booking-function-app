using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Abstractions.Persistence
{
    public interface IBookingRepository
    {
        Task<BookingsModel?> GetAsync(BookingId id, CancellationToken ct);
        Task SaveAsync(BookingsModel booking, CancellationToken ct);

        // New methods for richer queries
        Task<IReadOnlyList<BookingsModel>> GetScheduleAsync(
            string adviserId,
            DateTime startUtc,
            DateTime endUtc,
            CancellationToken ct);

        Task<IReadOnlyList<BookingsModel>> GetByCustomerAsync(
            string customerId,
            CancellationToken ct);

        Task<IReadOnlyList<BookingsModel>> GetByAdviserAsync(
            string adviserId,
            CancellationToken ct);
    }
}
