using AFH.Booking.Application.Calendar.Models;

namespace AFH.Booking.Application.Abstractions.Calendar;

public interface ICalendarSubscriptionStore
{
    Task UpsertAsync(CalendarSubscriptionEntity entity, CancellationToken ct);
    Task<CalendarSubscriptionEntity?> GetByAdviserIdAsync(string adviserId, CancellationToken ct);
    Task DeleteAsync(string adviserId, CancellationToken ct);
}