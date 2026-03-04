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

public sealed class GetAvailabilityQuery
{
    public string TransactionId { get; set; } = default!;
    public int DurationMinutes { get; set; }
    public bool IsRemote { get; set; }

    public PreferredStart PreferredStart { get; set; } = new();

    public DateTime? WindowStartUtc { get; set; }
    public DateTime? WindowEndUtc { get; set; }

    public int Limit { get; set; } = 10;

    public string? Cursor { get; set; }
    public string? MeetingType { get; set; }
}