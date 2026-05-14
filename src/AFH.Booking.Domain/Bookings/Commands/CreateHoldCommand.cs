namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class CreateHoldCommand
{
    public string SlotId { get; init; } = default!;
    public string? BookingId { get; init; }
    public string? TransactionRef { get; init; }
}
