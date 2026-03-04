namespace AFH.Booking.Application.Abstractions.Calendar.Subscription;
public interface ICreateSubscriptionHandler
{
    Task<Result<CreateCalendarSubscriptionResult>> HandleAsync(CreateCalendarSubscriptionRequest cmd, CancellationToken ct);
}
