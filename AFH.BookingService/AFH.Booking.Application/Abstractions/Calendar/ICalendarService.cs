using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Abstractions.Calendar;

public interface ICalendarService
{
    Task<string> CreateEventAsync(BookingsModel booking, CancellationToken ct);
    Task CancelEventAsync(string userId, string providerEventId, CancellationToken ct);
    Task<IReadOnlyList<CalendarScheduleItem>> GetScheduleAsync(string userId, DateTime startUtc, DateTime endUtc, CancellationToken ct);
}

public sealed class CalendarScheduleItem
{
    public string BookingId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string Status { get; init; } = string.Empty;
}
