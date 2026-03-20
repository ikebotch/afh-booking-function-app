namespace AFH.Booking.Contracts.V1.Requests;

public sealed class CancelBookingRequest
{
    public string BookingId { get; init; } = default!;
    public string? Reason { get; init; }
}
