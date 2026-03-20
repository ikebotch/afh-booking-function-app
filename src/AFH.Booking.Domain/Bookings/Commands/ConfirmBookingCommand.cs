namespace AFH.Booking.Domain.Bookings.Commands;
public sealed class ConfirmBookingCommand
{
    public string BookingId { get; set; } = default!;
    public string HoldId { get; set; } = default!;
    public string Notes { get; set; } = default!;
    public string TransactionIdOrClientId { get; set; } = default!;
}
