using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangeBookingService : IRearrangeBookingService
{
    private readonly IRearrangementOrchestrator _orchestrator;
    private readonly IApprovalWorkflowService _approvals;

    public RearrangeBookingService(
        IRearrangementOrchestrator orchestrator,
        IApprovalWorkflowService approvals)
    {
        _orchestrator = orchestrator;
        _approvals = approvals;
    }

    public async Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct)
    {
        var approvalResult = await EnsureAdviserApprovalAsync(cmd, ct);
        if (!approvalResult.IsSuccess)
            return Result<RearrangeBookingResponse>.Fail(
                approvalResult.StatusCode,
                approvalResult.ErrorMessage ?? "Approval is required.",
                approvalResult.ErrorCode);

        return await _orchestrator.RearrangeAsync(cmd, ct);
    }

    private async Task<Result> EnsureAdviserApprovalAsync(
        RearrangeBookingCommand cmd,
        CancellationToken ct)
    {
        if (!string.Equals(cmd.RequestedBy, LifecycleActors.Adviser, StringComparison.OrdinalIgnoreCase))
            return Result.Ok();

        if (string.IsNullOrWhiteSpace(cmd.ApprovalRequestId))
        {
            return Result.Fail(
                HttpStatusCode.Forbidden,
                "Adviser rearrangement requires an approved approvalRequestId.",
                "ApprovalRequired");
        }

        var approved = await _approvals.IsApprovedAsync(
            cmd.ApprovalRequestId.Trim(),
            cmd.BookingId.Trim(),
            changeType: "Rearrange",
            requestedBy: LifecycleActors.Adviser,
            ct: ct);

        return approved
            ? Result.Ok()
            : Result.Fail(
                HttpStatusCode.Forbidden,
                "Approval request is not approved for this booking rearrangement.",
                "ApprovalRequired");
    }
}
