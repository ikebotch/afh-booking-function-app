namespace AFH.Booking.Contracts.Responses;

public sealed class ConfirmBookingResponse
{
    public string ProviderEventId { get; init; }
    public string BookingId { get; init; }
    public string Status { get; init; } = "Confirmed";

}
