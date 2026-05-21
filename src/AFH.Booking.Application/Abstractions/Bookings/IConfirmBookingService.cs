using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IConfirmBookingService
{
    Task<Result<ConfirmBookingResponse>> HandleAsync(
        ConfirmBookingCommand cmd,
        CancellationToken ct);
}
