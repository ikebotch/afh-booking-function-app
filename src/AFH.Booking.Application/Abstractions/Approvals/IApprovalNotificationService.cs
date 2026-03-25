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
        string bookingId,
        string transactionId,
        string transactionRef,
        string requesterId,
        string approverId,
        string outcome,
        string changeType,
        string? notes,
        CancellationToken ct);
}
