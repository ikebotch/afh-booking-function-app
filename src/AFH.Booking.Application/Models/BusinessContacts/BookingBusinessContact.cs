using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Models.BusinessContacts;

public sealed record BookingBusinessContact(
    string ContactType,
    string DisplayName,
    string? Email,
    string? MobileNumber,
    IReadOnlyList<BookingNotificationChannel> Channels);
