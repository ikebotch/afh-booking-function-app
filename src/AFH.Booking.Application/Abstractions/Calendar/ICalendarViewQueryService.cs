using AFH.Booking.Application.Models.Calendar;

namespace AFH.Booking.Application.Abstractions.Calendar;

public interface ICalendarViewQueryService
{
    Task<Result<List<CalendarViewDto>>> HandleAsync(CalendarViewQuery q, CancellationToken ct);
}
