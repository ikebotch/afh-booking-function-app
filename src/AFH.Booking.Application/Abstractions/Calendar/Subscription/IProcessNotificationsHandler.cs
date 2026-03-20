using AFH.Booking.Contracts.V1.Requests;

namespace AFH.Booking.Application.Abstractions.Calendar.Subscription;
public interface IProcessNotificationsHandler
{
    Task<Result> HandleAsync(CalendarNotificationsRequest? envelope, CancellationToken ct);
}
