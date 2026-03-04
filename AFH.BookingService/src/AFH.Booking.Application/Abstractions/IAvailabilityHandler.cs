using AFH.Booking.Application.Bookings.Queries;
using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions;

public interface IAvailabilityHandler
{
    Task<Result<GetAvailabilityResponse>> HandleAsync(
        GetAvailabilityQuery query,
        CancellationToken ct);
}