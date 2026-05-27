using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Booking.Application.Models.Notifications;

public static class BookingNotificationTypes
{
    public static readonly NotificationType BookingConfirmed = new("Booking", "BookingConfirmed");
    public static readonly NotificationType BookingRescheduled = new("Booking", "BookingRescheduled");
    public static readonly NotificationType BookingCancelled = new("Booking", "BookingCancelled");
    public static readonly NotificationType BookingHoldCreated = new("Booking", "BookingHoldCreated");
}
