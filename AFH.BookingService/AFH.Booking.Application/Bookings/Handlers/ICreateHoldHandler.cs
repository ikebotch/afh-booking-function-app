using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Common;

namespace AFH.Booking.Application.Bookings.Handlers;
public interface ICreateHoldHandler
{
    Task<Result<object>> HandleAsync(CreateHoldModel command, CancellationToken ct);
}
