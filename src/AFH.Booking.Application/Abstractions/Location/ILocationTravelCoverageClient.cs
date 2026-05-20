using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Abstractions.Location;

public interface ILocationTravelCoverageClient
{
    Task<LocationTravelCoverageResult> EvaluateAsync(
        LocationTravelCoverageRequest request,
        CancellationToken ct);
}
