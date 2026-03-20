using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Persistence;

public interface ICalendarSubscriptionRepository
{
    Task<CalendarSubscription?> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken ct);
    Task UpsertAsync(CalendarSubscription subscription, CancellationToken ct);
}