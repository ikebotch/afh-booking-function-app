namespace AFH.Booking.Domain.Events;

public sealed class BookingConfirmed
{
    public string BookingId { get; set; } = default!;
    public DateTime ConfirmedUtc { get; set; }
}
