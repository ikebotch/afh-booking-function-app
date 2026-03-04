using AFH.Booking.Application.Calendar.Models;

namespace AFH.Booking.Application.Calendar.Subscriptions;

public interface ICalendarSubscriptionRepository
{
    Task<CalendarSubscriptionEntity?> GetByAdviserAsync(string adviserId, CancellationToken ct);
    Task<CalendarSubscriptionEntity?> GetByIdAsync(string subscriptionId, CancellationToken ct);
    Task UpsertAsync(CalendarSubscriptionEntity entity, CancellationToken ct);
    Task<IReadOnlyList<CalendarSubscriptionEntity>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<CalendarSubscriptionEntity>> ListExpiringWithinAsync(TimeSpan window, CancellationToken ct);
    Task DeleteAsync(string subscriptionId, CancellationToken ct);
}