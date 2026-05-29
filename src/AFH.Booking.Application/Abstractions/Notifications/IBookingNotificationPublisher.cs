using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Abstractions.Notifications;

public interface IBookingNotificationPublisher
{
    Task PublishAsync(BookingNotificationRequest notification, CancellationToken ct);
}
