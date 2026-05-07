namespace AFH.Booking.Contracts.V1.Requests;

public sealed class CreateBookingAccessLinkRequest
{
    public string? ActorId { get; init; }
    public string? CreatedBy { get; init; }
    public int? ExpiryHours { get; init; }
}
