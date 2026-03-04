using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Common;

namespace AFH.Booking.Application.Bookings.Handlers;

public interface ICancelBookingHandler
{
    Task<Result<object>> HandleAsync(CancelBookingModel command, CancellationToken ct);
}
