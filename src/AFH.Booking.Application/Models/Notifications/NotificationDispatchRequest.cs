namespace AFH.Booking.Application.Models.Notifications;

public sealed record NotificationDispatchRequest(
    string BookingId,
    string EventType,
    string? Message,
    bool SendSms,
    bool SendEmail,
    string? LifecycleEventId = null,
    string? CorrelationId = null);
