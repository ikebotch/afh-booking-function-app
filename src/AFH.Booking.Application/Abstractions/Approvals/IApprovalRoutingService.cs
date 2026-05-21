using AFH.Booking.Application.Models.Approvals;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IApprovalRoutingService
{
    Task<ApprovalRouteTarget> ResolveAsync(CancellationToken ct);
}
