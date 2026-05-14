using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IProspectResolver
{
    Task<(Domain.Client.ClientDirectoryItem? Value, Result<GetAvailabilityResponse>? Error)> ResolveAsync(
        GetAvailabilityQuery query,
        CancellationToken ct);
}
