namespace AFH.Booking.Application.Common.Clock;

public interface IClock
{
    DateTime UtcNow { get; }
}
