namespace AFH.Booking.Contracts.V1.Responses;

public sealed class CancelBookingResponse
{
    public string BookingId { get; init; } = default!;
    public DateTime CancelledUtc { get; init; }
    public string Status { get; init; } = "Cancelled";
}
