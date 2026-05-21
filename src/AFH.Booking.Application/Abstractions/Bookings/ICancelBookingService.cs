using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;
public interface ICancelBookingService
{
    Task<Result<CancelBookingResponse>> HandleAsync(
        CancelBookingCommand cmd,
        CancellationToken ct);
}