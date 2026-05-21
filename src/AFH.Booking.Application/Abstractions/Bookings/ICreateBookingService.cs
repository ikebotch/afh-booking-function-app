using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;
public interface ICreateBookingService
{
    Task<Result<CreateBookingResponse>> HandleAsync(
        CreateHoldCommand cmd,
        CancellationToken ct);
}
