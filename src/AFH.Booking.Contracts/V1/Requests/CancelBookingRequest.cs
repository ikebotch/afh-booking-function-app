namespace AFH.Booking.Contracts.V1.Requests;

public sealed class CancelBookingRequest
{
    public string BookingId { get; init; } = default!;
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public string? RequestedBy { get; init; }
    public string? ApprovalRequestId { get; init; }
    public string? Reason { get; init; }
}
