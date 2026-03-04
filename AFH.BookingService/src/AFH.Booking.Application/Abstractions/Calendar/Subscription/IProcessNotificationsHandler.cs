using AFH.Booking.Application.Calendar.Subscriptions;

namespace AFH.Booking.Application.Abstractions.Calendar.Subscription;
public interface IProcessNotificationsHandler
{
    Task<Result> HandleAsync(GraphNotificationEnvelope? envelope, CancellationToken ct);
}
