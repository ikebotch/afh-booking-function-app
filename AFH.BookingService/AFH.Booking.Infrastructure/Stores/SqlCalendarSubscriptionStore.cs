using AFH.Booking.Application.Calendar.Models;

namespace AFH.Booking.Infrastructure.Calendar.Stores;

public sealed class SqlCalendarSubscriptionStore : ICalendarSubscriptionStore
{
    public Task UpsertAsync(CalendarSubscriptionEntity entity, CancellationToken ct) => Task.CompletedTask;
    public Task<CalendarSubscriptionEntity?> GetByAdviserIdAsync(string adviserId, CancellationToken ct)
        => Task.FromResult<CalendarSubscriptionEntity?>(null);
    public Task DeleteAsync(string adviserId, CancellationToken ct) => Task.CompletedTask;
}