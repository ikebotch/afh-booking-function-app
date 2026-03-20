using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Contracts.V1.Dtos;

namespace AFH.Booking.Application.Abstractions.Calendar;

public interface ICalendarViewQueryHandler
{
    Task<Result<List<CalendarViewDto>>> HandleAsync(CalendarViewQuery q, CancellationToken ct);

}
