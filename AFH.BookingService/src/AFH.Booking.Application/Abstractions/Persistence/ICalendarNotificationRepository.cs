using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Persistence;

public interface ICalendarNotificationRepository
{
    Task<CalendarNotificationReceipt?> AddAsync(CalendarNotificationReceipt receipt, CancellationToken ct);

}