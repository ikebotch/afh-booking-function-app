namespace AFH.Booking.Application.Models.Calendar;

public sealed class CalendarViewResult
{
    public IReadOnlyList<AdviserCalendarWindow> Advisers { get; init; } = [];
}
