using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangeBookingService : IRearrangeBookingService
{
    private readonly IRearrangementOrchestrator _orchestrator;

    public RearrangeBookingService(IRearrangementOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct)
    {
        return await _orchestrator.RearrangeAsync(cmd, ct);
    }
}
