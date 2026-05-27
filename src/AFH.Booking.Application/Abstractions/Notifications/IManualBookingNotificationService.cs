using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Abstractions.Notifications;

public interface IManualBookingNotificationService
{
    Task<Result<NotificationDispatchResponse>> SendAsync(
        string bookingId,
        string eventType,
        string? messageOverride,
        bool sendSms,
        bool sendEmail,
        string? correlationId,
        CancellationToken ct);
}
