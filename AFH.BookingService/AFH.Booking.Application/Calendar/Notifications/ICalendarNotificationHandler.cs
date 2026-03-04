using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Webhooks;

namespace AFH.Booking.Application.Calendar.Notifications;

public interface ICalendarNotificationHandler
{
    Task<Result> HandleAsync(GraphNotificationEnvelope? envelope, CancellationToken ct);
}