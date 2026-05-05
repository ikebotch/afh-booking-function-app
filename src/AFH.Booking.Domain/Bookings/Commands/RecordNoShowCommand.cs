namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class RecordNoShowCommand
{
    public string BookingId { get; set; } = default!;
    public string? RequestedBy { get; set; }
    public string? ActorId { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonDetail { get; set; }
    public string? CorrelationId { get; set; }
}
