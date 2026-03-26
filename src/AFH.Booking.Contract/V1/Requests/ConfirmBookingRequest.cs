namespace AFH.Booking.Contracts.V1.Requests;

public sealed class ConfirmBookingRequest
{
    public string BookingId { get; init; } = default!;
}
