using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Responses;
using AFH.Common.CalendarUtils.Sdk.Contracts.Responses;

namespace AFH.Booking.Application.Calendar.Subscriptions;

public interface ICalendarSubscriptionService
{
    Task<Result<CreateCalendarSubscriptionResponse>> EnsureAsync(string adviserId, CancellationToken ct);
    Task<Result<IReadOnlyList<CalendarSubscriptionDto>>> ListAsync(CancellationToken ct);
    Task<Result<int>> RenewExpiringAsync(TimeSpan within, CancellationToken ct);
    Task<Result> DeleteAsync(string subscriptionId, CancellationToken ct);
}