using AFH.Booking.Contracts.V1.Responses;

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
