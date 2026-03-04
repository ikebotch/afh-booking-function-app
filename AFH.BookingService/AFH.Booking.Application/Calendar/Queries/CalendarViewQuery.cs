namespace AFH.Booking.Application.Calendar.Queries;

public sealed class CalendarViewQuery
{
    public IReadOnlyList<string> AdviserIds { get; init; } = [];
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
}