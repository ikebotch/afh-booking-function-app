using AFH.Booking.Application.Models.Approvals;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IApprovalNotificationService
{
    Task RecordRequestSubmittedAsync(
        ApprovalRouteTarget routeTarget,
        ApprovalWorkflowRecord approval,
        ApprovalBookingSnapshot booking,
        string requesterId,
        CancellationToken ct);

    Task RecordOutcomeAsync(
        ApprovalWorkflowRecord approval,
        ApprovalBookingSnapshot booking,
        string approverId,
        CancellationToken ct);
}
