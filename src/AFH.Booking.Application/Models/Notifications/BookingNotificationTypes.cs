namespace AFH.Booking.Application.Models.Notifications;

public static class BookingNotificationTypes
{
    public static readonly BookingNotificationType BookingConfirmed = new("Booking", "BookingConfirmed");
    public static readonly BookingNotificationType BookingRescheduled = new("Booking", "BookingRescheduled");
    public static readonly BookingNotificationType BookingCancelled = new("Booking", "BookingCancelled");
    public static readonly BookingNotificationType BookingHoldCreated = new("Booking", "BookingHoldCreated");
}
