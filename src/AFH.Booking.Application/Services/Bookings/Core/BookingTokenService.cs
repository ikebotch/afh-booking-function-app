using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AFH.Booking.Application.Services.Bookings.Core;

public sealed class BookingTokenService : IBookingTokenService
{
    private readonly IBookingChangeAccessService _accessService;
    private readonly ILifecycleAuditService _audit;

    public BookingTokenService(
        IBookingChangeAccessService accessService,
        ILifecycleAuditService audit)
    {
        _accessService = accessService;
        _audit = audit;
    }

    public async Task<Result<string>> GenerateClientAccessTokenAsync(string bookingId, CancellationToken ct)
    {
        var result = await _accessService.GenerateClientTokenAsync(bookingId, ct);
        
        // Ensure audit logic is invoked here for issuance if necessary
        // _audit.RecordEventAsync(...) if token issuance is considered an audited business event.

        return result;
    }
}
