using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAdviserPoolBuilder
{
    Task<(AdviserPoolResult Value, Result<GetAvailabilityResponse>? Error)> BuildAsync(
        GetAvailabilityQuery query,
        Domain.Client.ClientDirectoryItem? prospect,
        CancellationToken ct);
}
