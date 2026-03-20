using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IApprovalWorkflowService
{
    Task<ApprovalRequestResponse> CreateAsync(
        string bookingId,
        string changeType,
        string requestedBy,
        string? reasonCode,
        string? reasonDetail,
        CancellationToken ct);

    Task<IReadOnlyList<ApprovalRequestResponse>> ListPendingAsync(CancellationToken ct);

    Task<ApprovalRequestResponse?> GetAsync(string requestId, CancellationToken ct);

    Task<ApprovalRequestResponse?> ReviewAsync(
        string requestId,
        bool approved,
        string reviewer,
        string? notes,
        CancellationToken ct);

    Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct);
}
