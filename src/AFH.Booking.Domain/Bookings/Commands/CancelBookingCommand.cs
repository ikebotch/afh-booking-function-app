namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class CancelBookingCommand
{
    public string BookingId { get; set; } = default!;
    public string? Reason { get; set; }
}
