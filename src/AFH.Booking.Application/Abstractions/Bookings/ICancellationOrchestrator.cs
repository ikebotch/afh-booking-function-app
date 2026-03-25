using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface ICancellationOrchestrator
{
    Task<Result<CancelBookingResponse>> CancelAsync(
        CancelBookingCommand command,
        bool sendClientNotification,
        CancellationToken ct);
}
