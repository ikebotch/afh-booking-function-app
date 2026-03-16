using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions.Bookings.Handlers;

public interface IRearrangementHandler
{
    Task<Result<GetRearrangementOptionsResponse>> GetOptionsAsync(
        string bookingId,
        GetRearrangementOptionsRequest request,
        CancellationToken ct);

    Task<Result<ExecuteRearrangementResponse>> ExecuteAsync(
        string bookingId,
        ExecuteRearrangementRequest request,
        CancellationToken ct);
}
