using AFH.Booking.Domain.Common;

namespace AFH.Booking.Domain.Bookings;

public static class HoldWindow
{
    // Centralises hold expiry behaviour (easy to change later)
    public static DateTime ComputeHoldExpiryUtc(DateTime utcNow, TimeSpan duration)
    {
        Guard.True(duration > TimeSpan.Zero, "Hold duration must be > 0.");
        return utcNow.Add(duration);
    }
}
