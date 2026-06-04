namespace AFH.Booking.Contracts.V1.Requests;

public sealed class CreateApprovalRequest
{
    public string ChangeType { get; init; } = "Rearrange"; // Cancel | Rearrange
    public string RequestedBy { get; init; } = "Adviser"; // Adviser
    public string? RequesterId { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public string? NewSlotId { get; init; }
    public string? AdviserNote { get; init; }
    public IReadOnlyList<ApprovalProposedAlternativeTimeRequest> ProposedAlternativeTimes { get; init; } = [];
}

public sealed class ApprovalProposedAlternativeTimeRequest
{
    public string? SlotId { get; init; }
    public string? AdviserId { get; init; }
    public DateTime? StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
    public string? Note { get; init; }
    public int? PreferenceOrder { get; init; }
}
