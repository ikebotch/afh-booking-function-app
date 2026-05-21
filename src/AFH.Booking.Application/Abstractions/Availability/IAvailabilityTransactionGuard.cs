using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilityTransactionGuard
{
    Task<Result<GetAvailabilityResponse>?> EnsureOpenAsync(GetAvailabilityQuery query, CancellationToken ct);
}
