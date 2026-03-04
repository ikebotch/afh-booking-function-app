namespace AFH.Booking.Contracts.Responses;

public sealed class CancelBookingResponse
{
    public string ProviderEventId { get; init; }
    public string BookingId { get; init; }
    public bool Cancelled { get; init; }

}
