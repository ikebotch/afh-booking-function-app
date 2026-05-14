using AFH.Booking.Contracts.V1.Dtos;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Calendar;

public interface ICalendarViewQueryHandler
{
    Task<Result<List<CalendarViewDto>>> HandleAsync(CalendarViewQuery q, CancellationToken ct);

}
