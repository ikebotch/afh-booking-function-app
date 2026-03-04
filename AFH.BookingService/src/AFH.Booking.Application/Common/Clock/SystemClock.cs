namespace AFH.Booking.Application.Common.Clock;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
