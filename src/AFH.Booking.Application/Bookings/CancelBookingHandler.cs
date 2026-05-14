using System.Text.Json;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class CancelBookingHandler : ICancelBookingHandler
{
    private readonly ICancellationOrchestrator _orchestrator;

    public CancelBookingHandler(ICancellationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<Result<CancelBookingResponse>> HandleAsync(
        CancelBookingCommand cmd,
        CancellationToken ct)
    {
        return await _orchestrator.CancelAsync(cmd, sendClientNotification: true, ct);
    }
}
