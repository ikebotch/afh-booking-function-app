namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class RearrangeBookingCommand
{
    public string BookingId { get; init; } = default!;
    public string NewSlotId { get; init; } = default!;
    public string RequestedBy { get; init; } = "Client";
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
}
