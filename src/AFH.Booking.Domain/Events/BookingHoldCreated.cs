namespace AFH.Booking.Domain.Events;

public sealed class BookingHoldCreated
{
    public string BookingId { get; set; } = default!;
    public string AdviserId { get; set; } = default!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}
