namespace AFH.Booking.Contracts.V1.Requests;

public sealed class CreateApprovalRequest
{
    public string ChangeType { get; init; } = "Rearrange"; // Cancel | Rearrange
    public string RequestedBy { get; init; } = "Adviser"; // Adviser
    public string? RequesterId { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public string? NewSlotId { get; init; }
}
