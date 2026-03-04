using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Common;

namespace AFH.Booking.Application.Bookings.Handlers;
public interface IConfirmBookingHandler
{
    Task<Result<object>> HandleAsync(ConfirmBookingModel command, CancellationToken ct);
}
