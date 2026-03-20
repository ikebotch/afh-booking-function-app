namespace AFH.Booking.Domain.Calendar;

public sealed class CalendarEventDetails
{
    public string CalendarId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string? ChangeKey { get; init; }
    public string? ICalUId { get; init; }
}
