using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Abstractions.Location;

public interface ILocationRouteTimeClient
{
    Task<LocationRouteTimeResult> CalculateAsync(
        LocationRouteTimeRequest request,
        CancellationToken ct);
}
