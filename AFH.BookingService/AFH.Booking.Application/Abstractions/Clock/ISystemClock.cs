namespace AFH.Booking.Application.Abstractions.Clock;

public interface ISystemClock
{
    DateTime UtcNow { get; }
}
