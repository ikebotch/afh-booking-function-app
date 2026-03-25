using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IApprovalWorkflowService
{
    Task<ApprovalRequestResponse> CreateAsync(CreateApprovalWorkflowRequest request, CancellationToken ct);

    Task<IReadOnlyList<ApprovalRequestResponse>> ListPendingAsync(CancellationToken ct);

    Task<ApprovalRequestResponse?> GetAsync(string requestId, CancellationToken ct);

    Task<ApprovalRequestResponse?> ReviewAsync(ReviewApprovalWorkflowRequest request, CancellationToken ct);

    Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct);
}
