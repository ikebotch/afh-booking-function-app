namespace AFH.Booking.Contracts.V1.Responses;

public sealed class ApprovalRequestResponse
{
    public string RequestId { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string TransactionId { get; init; } = default!;
    public string? ClientName { get; init; }
    public string? AdviserName { get; init; }
    public string? MeetingType { get; init; }
    public IReadOnlyList<string> Skills { get; init; } = Array.Empty<string>();
    public string ChangeType { get; init; } = default!;
    public string RequestedBy { get; init; } = default!;
    public string? RequesterId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime RequestedUtc { get; init; }
    public IReadOnlyList<string> RoutedTo { get; init; } = Array.Empty<string>();
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public string? NewSlotId { get; init; }
    public IReadOnlyList<ApprovalRequestNoteResponse> Notes { get; init; } = Array.Empty<ApprovalRequestNoteResponse>();
    public IReadOnlyList<ApprovalProposedAlternativeTimeResponse> ProposedAlternativeTimes { get; init; } = Array.Empty<ApprovalProposedAlternativeTimeResponse>();
    public string? ApproverTargetType { get; init; }
    public string? ApproverTargetValue { get; init; }
    public string? ApproverTargetDisplayName { get; init; }
    public string? Reviewer { get; init; }
    public DateTime? ReviewedUtc { get; init; }
    public string? ReviewNotes { get; init; }
    public DateTime? ExecutedUtc { get; init; }
}

public sealed class ApprovalRequestNoteResponse
{
    public string Id { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string ApprovalRequestId { get; init; } = default!;
    public string ActorType { get; init; } = default!;
    public string? ActorId { get; init; }
    public string? DisplayName { get; init; }
    public string Text { get; init; } = default!;
    public DateTime CreatedUtc { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed class ApprovalProposedAlternativeTimeResponse
{
    public string? SlotId { get; init; }
    public string? AdviserId { get; init; }
    public DateTime? StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
    public string? Note { get; init; }
    public int? PreferenceOrder { get; init; }
}
