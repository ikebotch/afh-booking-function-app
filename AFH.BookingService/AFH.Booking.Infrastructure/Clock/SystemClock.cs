namespace AFH.Booking.Infrastructure.Clock;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
