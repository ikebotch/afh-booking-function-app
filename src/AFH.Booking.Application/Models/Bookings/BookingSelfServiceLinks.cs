namespace AFH.Booking.Application.Models.Bookings;

public sealed record BookingSelfServiceLinks(
    string ViewBookingUrl,
    string CancelBookingUrl,
    string RescheduleBookingUrl);
