using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IRearrangeBookingService
{
    Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct);
}
