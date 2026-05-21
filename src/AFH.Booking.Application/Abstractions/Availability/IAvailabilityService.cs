using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilityService
{
    Task<Result<GetAvailabilityResponse>> HandleAsync(
        GetAvailabilityQuery query,
        CancellationToken ct);
}
