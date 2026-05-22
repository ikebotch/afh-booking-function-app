namespace AFH.Booking.Application.Models.Calendar;

public sealed class CalendarShowAsRemediationResult
{
    public string BookingId { get; init; } = default!;
    public string EventId { get; init; } = default!;
    public string ShowAs { get; init; } = default!;
    public DateTime RemediatedUtc { get; init; }
}
