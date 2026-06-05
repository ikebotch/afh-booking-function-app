namespace AFH.Booking.Application.Models.Calendar;

public sealed class CalendarShowAsRemediationResult
{
    public string BookingId { get; init; } = default!;
    public string EventId { get; init; } = default!;
    public string? PreviousEventId { get; init; }
    public string ShowAs { get; init; } = default!;
    public bool RestoredMissingEvent { get; init; }
    public DateTime RemediatedUtc { get; init; }
}
