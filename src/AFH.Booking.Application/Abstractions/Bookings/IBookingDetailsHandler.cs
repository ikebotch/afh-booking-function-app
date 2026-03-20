using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings.Handlers;

public interface IBookingDetailsHandler
{
    Task<Result<BookingDetailsResponse>> HandleAsync(GetBookingDetailsQuery query, CancellationToken ct);
}
