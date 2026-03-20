using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings.Handlers;
public interface ICancelBookingHandler
{
    Task<Result<CancelBookingResponse>> HandleAsync(
        CancelBookingCommand cmd,
        CancellationToken ct);
}