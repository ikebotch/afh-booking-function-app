namespace AFH.Booking.Application.Abstractions.Calendar;

public interface IBookingShowAsRemediationService
{
    Task<Result<CalendarShowAsRemediationResult>> HandleAsync(string bookingId, CancellationToken ct);
}
