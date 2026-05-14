namespace AFH.Booking.Domain.ValueObjects;

public static class HoldWindow
{
    public static DateTime ComputeHoldExpiryUtc(DateTime utcNow, TimeSpan duration)
    {
        Guard.True(duration > TimeSpan.Zero, "Hold duration must be > 0.");

        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        return utcNow.Add(duration);
    }
}
