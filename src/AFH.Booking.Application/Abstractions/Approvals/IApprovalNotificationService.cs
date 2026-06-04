using AFH.Booking.Application.Models.Approvals;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IApprovalNotificationService
{
    Task RecordRequestSubmittedAsync(
        ApprovalRouteTarget routeTarget,
        string bookingId,
        string transactionId,
        string transactionRef,
        string requesterId,
        string changeType,
        string reasonCode,
        string? reasonDetail,
        CancellationToken ct);

    Task RecordOutcomeAsync(
        string requestId,
        string bookingId,
        string transactionId,
        string transactionRef,
        string requesterId,
        string approverId,
        string outcome,
        string changeType,
        string? reasonCode,
        string? reasonDetail,
        string? notes,
        CancellationToken ct);
}
