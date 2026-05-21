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

public sealed record SelectedSlotRouteTimeGuardResult(
    bool IsAllowed,
    bool WasTriggered,
    string? ErrorMessage,
    string? ErrorCode,
    int? TravelTimeMinutes,
    double? TravelDistanceMiles);
