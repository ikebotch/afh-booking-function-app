
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Responses;

namespace AFH.Booking.Application.Calendar.Queries;
public interface ICalendarViewHandler
{
    Task<Result<CalendarViewDto>> HandleAsync(CalendarViewQuery query, CancellationToken ct);
}
