using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Persistence;

public interface ICalendarGateway
{


    Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct);

    Task CancelBookingEventAsync(
     string userId,
     string providerEventId,
     CancellationToken ct);
    Task<string?> UpdateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct);

    Task<CalendarEventDetails?> GetEventAsync(string userId, string eventId, CancellationToken ct = default);

    Task<AdviserAvailabilityResult> CheckAvailabilityAsync(
        string userId,
        DateTime startUtc,
        DateTime endUtc,
        string timezone,
        string? freshnessMode,
        CancellationToken ct);
}


public sealed class CalendarNotFoundException : Exception
{
    public CalendarNotFoundException(string message) : base(message) { }
}
