using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Availability;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilityHandler
{
    Task<Result<GetAvailabilityResponse>> HandleAsync(
        GetAvailabilityQuery query,
        CancellationToken ct);
}