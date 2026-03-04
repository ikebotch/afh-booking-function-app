namespace AFH.Booking.Domain.ValueObjects;

public sealed class TimeRange
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public TimeSpan Duration => EndUtc - StartUtc;

    public TimeRange(DateTime startUtc, DateTime endUtc)
    {
        startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        endUtc   = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);

        Guard.True(endUtc > startUtc, "EndUtc must be after StartUtc.");

        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public bool Overlaps(TimeRange other)
        => StartUtc < other.EndUtc && other.StartUtc < EndUtc;
}
