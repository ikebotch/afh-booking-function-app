namespace AFH.Booking.Application.Models.Calendar;

public sealed class CalendarEventDetails
{
    public string CalendarId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string? ChangeKey { get; init; }
    public string? ICalUId { get; init; }
    public string? ShowAs { get; init; }
    public bool HasLocation { get; init; }
    public string? LocationDisplayName { get; init; }
    public bool IsRecurring { get; init; }
    public string? RecurrencePattern { get; init; }
}
