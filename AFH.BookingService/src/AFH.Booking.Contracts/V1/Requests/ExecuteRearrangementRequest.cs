namespace AFH.Booking.Contracts.V1.Requests;

public sealed class ExecuteRearrangementRequest
{
    public string NewSlotId { get; init; } = default!;
    public string? TransactionRef { get; init; }
    public string ActorRole { get; init; } = "Client";
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public bool ApprovalGranted { get; init; }
    public string? ApprovedBy { get; init; }
}
