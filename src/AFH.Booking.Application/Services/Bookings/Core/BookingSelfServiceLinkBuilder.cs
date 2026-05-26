using AFH.Booking.Application.Models.Bookings;

namespace AFH.Booking.Application.Bookings;

internal static class BookingSelfServiceLinkBuilder
{
    public static BookingSelfServiceLinks? Build(string? clientPortalBaseUrl, string bookingId, string? token)
    {
        if (string.IsNullOrWhiteSpace(clientPortalBaseUrl) ||
            string.IsNullOrWhiteSpace(bookingId) ||
            string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var baseUrl = clientPortalBaseUrl.TrimEnd('/');
        var encodedBookingId = Uri.EscapeDataString(bookingId.Trim());
        var encodedToken = Uri.EscapeDataString(token.Trim());

        return new BookingSelfServiceLinks(
            ViewBookingUrl: $"{baseUrl}/bookings/{encodedBookingId}?token={encodedToken}",
            CancelBookingUrl: $"{baseUrl}/bookings/{encodedBookingId}/cancel?token={encodedToken}",
            RescheduleBookingUrl: $"{baseUrl}/bookings/{encodedBookingId}/reschedule?token={encodedToken}");
    }
}
