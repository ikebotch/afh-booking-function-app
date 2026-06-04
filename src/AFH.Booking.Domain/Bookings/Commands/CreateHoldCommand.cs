namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class CreateHoldCommand
{
    public string SlotId { get; init; } = default!;
    public string? TransactionRef { get; init; }
    public BookingActorContext? ActorContext { get; init; }
}
