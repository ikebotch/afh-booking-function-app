using AFH.Booking.Application.Models.Approvals;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IApprovalWorkflowStore
{
    Task<ApprovalBookingSnapshot> LoadBookingAsync(string bookingId, CancellationToken ct);

    Task AddRequestAsync(
        ApprovalWorkflowRecord request,
        ApprovalHistoryRecord history,
        CancellationToken ct);

    Task<IReadOnlyList<ApprovalWorkflowRecord>> ListPendingAsync(CancellationToken ct);

    Task<IReadOnlyList<ApprovalWorkflowRecord>> ListAsync(
        ListApprovalWorkflowRequestsQuery query,
        CancellationToken ct);

    Task<bool> HasPendingRequestAsync(
        string bookingId,
        string? bookingReference,
        string changeType,
        string requestedBy,
        string? requesterId,
        CancellationToken ct);

    Task<ApprovalWorkflowRecord?> GetAsync(string requestId, CancellationToken ct);

    Task<ApprovalWorkflowRecord?> GetForUpdateAsync(string requestId, CancellationToken ct);

    Task UpdateAsync(ApprovalWorkflowRecord request, CancellationToken ct);

    Task AddHistoryAsync(ApprovalHistoryRecord history, CancellationToken ct);

    Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct);
}
