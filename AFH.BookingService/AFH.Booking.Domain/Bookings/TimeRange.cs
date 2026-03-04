using AFH.Booking.Domain.Common;

namespace AFH.Booking.Domain.Bookings;

public readonly record struct TimeRange(DateTime StartUtc, DateTime EndUtc)
{
    public static TimeRange Create(DateTime startUtc, DateTime endUtc)
    {
        Guard.True(endUtc > startUtc, "EndUtc must be after StartUtc.");
        return new TimeRange(startUtc, endUtc);
    }

    public bool Overlaps(TimeRange other)
        => StartUtc < other.EndUtc && other.StartUtc < EndUtc;
}
