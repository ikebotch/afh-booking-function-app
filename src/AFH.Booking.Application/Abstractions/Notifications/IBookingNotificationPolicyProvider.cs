using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Abstractions.Notifications;

public interface IBookingNotificationPolicyProvider
{
    Task<BookingNotificationPolicy> GetAsync(
        string sourceApplication,
        BookingNotificationType notificationType,
        CancellationToken ct);
}
