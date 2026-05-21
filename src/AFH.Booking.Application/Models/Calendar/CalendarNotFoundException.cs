namespace AFH.Booking.Application.Models.Calendar;

public sealed class CalendarNotFoundException : Exception
{
    public CalendarNotFoundException(string message) : base(message) { }
}
