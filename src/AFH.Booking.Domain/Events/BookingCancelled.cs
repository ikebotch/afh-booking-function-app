namespace AFH.Booking.Domain.Events;

public sealed class BookingCancelled
{
    public string BookingId { get; set; } = default!;
    public string? Reason { get; set; }
}
