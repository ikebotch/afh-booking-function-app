using AFH.Booking.Application.Models.Notifications;
using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Booking.Application.Abstractions.Notifications;

public interface IBookingNotificationPolicyProvider
{
    Task<BookingNotificationPolicy> GetAsync(
        string sourceApplication,
        NotificationType notificationType,
        CancellationToken ct);
}
