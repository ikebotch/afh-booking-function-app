namespace AFH.Booking.Domain.Bookings.Commands;


public sealed class ReleaseHoldCommand
{
    public string HoldId { get; init; } = default!;
    public BookingActorContext? ActorContext { get; init; }
}
