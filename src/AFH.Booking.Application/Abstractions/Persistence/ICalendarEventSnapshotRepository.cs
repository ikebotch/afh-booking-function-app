using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Persistence;


public interface ICalendarEventSnapshotRepository
{
    Task AddAsync(CalendarEventSnapshot snapshot, CancellationToken ct);
    Task<CalendarEventSnapshot?> GetLatestAsync(string userId, string providerEventId, CancellationToken ct);
}
