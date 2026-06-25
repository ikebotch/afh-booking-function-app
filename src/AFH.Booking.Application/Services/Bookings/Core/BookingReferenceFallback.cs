namespace AFH.Booking.Application.Services.Bookings.Core;

internal static class BookingReferenceFallback
{
    public static string CreateBookingReference(string bookingId)
        => $"BK-{CreateSuffix(bookingId)}";

    private static string CreateSuffix(string value)
    {
        var source = string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value;

        var suffix = new string(source
            .Where(char.IsLetterOrDigit)
            .TakeLast(8)
            .ToArray());

        return string.IsNullOrWhiteSpace(suffix)
            ? Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
            : suffix.ToUpperInvariant();
    }
}
