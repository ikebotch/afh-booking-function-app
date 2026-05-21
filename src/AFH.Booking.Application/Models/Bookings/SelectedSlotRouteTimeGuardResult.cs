namespace AFH.Booking.Application.Models.Bookings;

public sealed record SelectedSlotRouteTimeGuardResult(
    bool IsAllowed,
    bool WasTriggered,
    string? ErrorMessage,
    string? ErrorCode,
    int? TravelTimeMinutes,
    double? TravelDistanceMiles);
