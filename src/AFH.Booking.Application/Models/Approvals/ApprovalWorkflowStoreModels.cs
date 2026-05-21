using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Models.Approvals;

public sealed record ApprovalBookingSnapshot(
    BookingHold Hold,
    BookingSlot Slot,
    BookingTransaction Transaction);

public sealed class ApprovalWorkflowRecord
{
    public string Id { get; set; } = default!;
    public string BookingId { get; set; } = default!;
    public string TransactionId { get; set; } = default!;
    public string ChangeType { get; set; } = default!;
    public string RequestedBy { get; set; } = default!;
    public string? RequesterId { get; set; }
    public string Status { get; set; } = default!;
    public DateTime RequestedUtc { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonDetail { get; set; }
    public string? RequestedPayloadJson { get; set; }
    public string? ApproverTargetType { get; set; }
    public string? ApproverTargetValue { get; set; }
    public string? ApproverTargetDisplayName { get; set; }
    public string? Reviewer { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public DateTime? ExecutedUtc { get; set; }
    public string? ExecutionError { get; set; }
}

public sealed class ApprovalHistoryRecord
{
    public string Id { get; set; } = default!;
    public string ApprovalRequestId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string ActorType { get; set; } = default!;
    public string? ActorId { get; set; }
    public string Outcome { get; set; } = default!;
    public string? Comments { get; set; }
    public DateTime OccurredUtc { get; set; }
}
