using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Abstractions.Location;

public interface ISelectedSlotRouteTimeGuard
{
    Task<SelectedSlotRouteTimeGuardResult> EvaluateAsync(
        BookingSlot slot,
        BookingTransaction transaction,
        string holdId,
        CancellationToken ct);
}
