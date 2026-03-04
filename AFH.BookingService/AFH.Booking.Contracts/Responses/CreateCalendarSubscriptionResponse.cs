using AFH.Booking.Contracts.Responses;

namespace AFH.Common.CalendarUtils.Sdk.Contracts.Responses;

public sealed class CreateCalendarSubscriptionResponse
{
    public CalendarSubscriptionDto? Subscription { get; set; }
    public bool Created { get; set; }
}
