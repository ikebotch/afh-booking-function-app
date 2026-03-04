using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Abstractions.Calendar;

public interface ICalendarService
{
    Task<string> CreateEventAsync(BookingsModel booking, CancellationToken ct);
    Task CancelEventAsync(string userId, string providerEventId, CancellationToken ct);
}
