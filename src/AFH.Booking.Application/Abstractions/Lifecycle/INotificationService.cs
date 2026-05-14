using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface INotificationService
{
    Task<NotificationDispatchResponse> SendBookingNotificationAsync(
        NotificationDispatchRequest request,
        CancellationToken ct);
}
