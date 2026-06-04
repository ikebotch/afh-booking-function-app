namespace AFH.Booking.Domain.Bookings.Commands;


public enum ReleaseHoldKind
{
    ManualRelease = 0,
    Expiry = 1
}

public sealed class ReleaseHoldCommand
{
    public string HoldId { get; init; } = default!;
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public ReleaseHoldKind ReleaseKind { get; init; } = ReleaseHoldKind.ManualRelease;
    public BookingActorContext? ActorContext { get; init; }
}
