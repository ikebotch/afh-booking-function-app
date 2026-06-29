namespace AFH.Booking.Contracts.V1.Responses;

public sealed class CancelBookingResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public DateTime CancelledUtc { get; init; }
    public string Status { get; init; } = "Cancelled";
}
