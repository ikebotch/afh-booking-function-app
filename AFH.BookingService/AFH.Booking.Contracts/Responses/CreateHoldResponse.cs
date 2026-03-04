namespace AFH.Booking.Contracts.Responses;

public sealed class CreateHoldResponse
{
    public string BookingId { get; set; } = default!;
    public string ProviderEventId { get; set; } = default!;
    public DateTime HoldExpiresUtc { get; set; }
}