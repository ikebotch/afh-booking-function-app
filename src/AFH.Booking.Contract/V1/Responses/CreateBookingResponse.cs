namespace AFH.Booking.Contracts.V1.Responses;

public sealed class CreateBookingResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string SlotId { get; init; } = default!;
    public DateTime HoldExpiresUtc { get; init; }
    public int CompanyBufferMinutes { get; init; }
}
