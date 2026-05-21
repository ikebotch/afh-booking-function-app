using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangeBookingHandler : IRearrangeBookingHandler
{
    private readonly IRearrangementOrchestrator _orchestrator;

    public RearrangeBookingHandler(IRearrangementOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct)
    {
        return await _orchestrator.RearrangeAsync(cmd, ct);
    }
}
