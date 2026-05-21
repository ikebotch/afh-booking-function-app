using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;
public interface IReleaseHoldService
{
    Task<Result<ReleaseHoldResponse>> HandleAsync(string holdId, CancellationToken ct);
}