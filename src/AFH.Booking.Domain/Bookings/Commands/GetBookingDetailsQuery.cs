namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class GetBookingDetailsQuery
{
    public string BookingId { get; set; } = default!;
}
