namespace AFH.Booking.Domain.Availability;


public enum PreferredStartKind
{
    None = 0,
    DateOnly = 1,
    DateTimeUtc = 2
}


public sealed class PreferredStart
{
    public PreferredStartKind Kind { get; set; } = PreferredStartKind.None;
    public DateOnly? DateUtc { get; set; }
    public DateTime? StartUtc { get; set; } // UTC
}
