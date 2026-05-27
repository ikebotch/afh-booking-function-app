using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Policies.Booking;

public sealed class BookingNotificationTemplatePolicy : INotificationTemplatePolicy
{
    public bool CanHandle(NotificationType notificationType)
        => string.Equals(notificationType.SourceApplication, "Booking", StringComparison.OrdinalIgnoreCase);

    public string GetTemplateName(NotificationType notificationType)
        => notificationType.Name switch
        {
            "BookingConfirmed" => "Booking.booking-confirmed.v1.txt",
            "BookingRescheduled" => "Booking.booking-rescheduled.v1.txt",
            "BookingCancelled" => "Booking.booking-cancelled.v1.txt",
            "BookingHoldCreated" => "Booking.booking-hold.v1.txt",
            _ => throw new NotSupportedException($"Booking notification template '{notificationType.Name}' is not supported yet.")
        };
}
