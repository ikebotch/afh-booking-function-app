using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Models.Approvals;

public sealed record CreateApprovalWorkflowRequest(
    string BookingId,
    string ChangeType,
    string RequestedBy,
    string? RequesterId,
    string? ReasonCode,
    string? ReasonDetail,
    string? NewSlotId,
    string? CorrelationId,
    BookingActorContext? ActorContext = null,
    string? AdviserNote = null,
    IReadOnlyList<ApprovalProposedAlternativeTime>? ProposedAlternativeTimes = null);

public sealed record ReviewApprovalWorkflowRequest(
    string RequestId,
    bool Approved,
    string Reviewer,
    string? Notes,
    string? CorrelationId,
    BookingActorContext? ActorContext = null,
    string? SelectedSlotId = null);

public sealed record ListApprovalWorkflowRequestsQuery(
    string? RequesterId,
    IReadOnlyList<string> BookingIds,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> ChangeTypes);

public sealed record ApprovalRouteTarget(
    string TargetType,
    string TargetValue,
    string DisplayName);

public sealed class ApprovalRequestResponse
{
    public string RequestId { get; init; } = default!;
    public string? RequestReference { get; init; }
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string TransactionId { get; init; } = default!;
    public string ChangeType { get; init; } = default!;
    public string RequestedBy { get; init; } = default!;
    public string? RequesterId { get; init; }
    public string Status { get; init; } = default!;
    public DateTime RequestedUtc { get; init; }
    public IReadOnlyList<string> RoutedTo { get; init; } = Array.Empty<string>();
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public string? NewSlotId { get; init; }
    public IReadOnlyList<ApprovalRequestNoteResponse> Notes { get; init; } = [];
    public IReadOnlyList<ApprovalProposedAlternativeTime> ProposedAlternativeTimes { get; init; } = [];
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

public sealed class ApprovalProposedAlternativeTime
{
    public string? SlotId { get; init; }
    public string? AdviserId { get; init; }
    public DateTime? StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
    public string? Note { get; init; }
    public int? PreferenceOrder { get; init; }
}

public sealed class EmailBounceWebhookRequest
{
    public string? ProviderMessageId { get; init; }
    public string? RecipientEmail { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public DateTime? OccurredUtc { get; init; }
}

public sealed class EmailBounceEventResponse
{
    public string BounceId { get; init; } = default!;
    public string? ProviderMessageId { get; init; }
    public string? RecipientEmail { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public DateTime OccurredUtc { get; init; }
    public DateTime ReceivedUtc { get; init; }
}
