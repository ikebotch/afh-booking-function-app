namespace AFH.Booking.Contracts.V1.Requests;

public sealed class RearrangeBookingRequest
{
    public string NewSlotId { get; init; } = default!;
    public string RequestedBy { get; init; } = "Client"; // Client | Adviser | Partner
    public string? ApprovalRequestId { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
}
