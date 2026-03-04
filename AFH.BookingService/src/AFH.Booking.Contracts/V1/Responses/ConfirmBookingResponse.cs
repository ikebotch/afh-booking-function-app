namespace AFH.Booking.Contracts.V1.Responses;

public sealed class ConfirmBookingResponse
{
    public string BookingId { get; init; } = default!;
    public string SlotId { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? OnlineMeetingJoinUrl { get; init; }
}
