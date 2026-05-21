using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IRearrangementOptionsService
{
    Task<Result<RearrangementOptionsResponse>> HandleAsync(GetRearrangementOptionsCommand cmd, CancellationToken ct);
}
