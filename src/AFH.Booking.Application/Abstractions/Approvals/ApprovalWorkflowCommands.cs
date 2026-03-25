namespace AFH.Booking.Application.Abstractions.Approvals;

public sealed record CreateApprovalWorkflowRequest(
    string BookingId,
    string ChangeType,
    string RequestedBy,
    string? RequesterId,
    string? ReasonCode,
    string? ReasonDetail,
    string? NewSlotId,
    string? CorrelationId);

public sealed record ReviewApprovalWorkflowRequest(
    string RequestId,
    bool Approved,
    string Reviewer,
    string? Notes,
    string? CorrelationId);

public sealed record ApprovalRouteTarget(
    string TargetType,
    string TargetValue,
    string DisplayName);
