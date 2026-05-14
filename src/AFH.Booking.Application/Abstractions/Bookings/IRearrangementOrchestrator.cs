using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IRearrangementOrchestrator
{
    Task<Result<RearrangeBookingResponse>> RearrangeAsync(RearrangeBookingCommand command, CancellationToken ct);
}
