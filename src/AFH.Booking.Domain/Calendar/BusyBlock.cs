namespace AFH.Booking.Domain.Calendar;

public sealed class BusyBlock
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
}