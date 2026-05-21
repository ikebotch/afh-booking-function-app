using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface INotificationService
{
    Task<NotificationDispatchResponse> SendBookingNotificationAsync(
        NotificationDispatchRequest request,
        CancellationToken ct);
}
