using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Persistence;

public interface ICalendarNotificationRepository
{
    Task<CalendarNotificationReceipt?> AddAsync(CalendarNotificationReceipt receipt, CancellationToken ct);
    Task<bool> ExistsRecentDuplicateAsync(
        string subscriptionId,
        string eventId,
        string? changeType,
        DateTime sinceUtc,
        CancellationToken ct);
}
