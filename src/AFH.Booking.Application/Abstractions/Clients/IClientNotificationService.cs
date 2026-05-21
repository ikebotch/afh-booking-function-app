using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IClientNotificationService
{
    Task<NotificationDispatchResponse> SendBookingNotificationAsync(
        string bookingId,
        string eventType,
        string? message,
        bool sendSms,
        bool sendEmail,
        CancellationToken ct);
}
