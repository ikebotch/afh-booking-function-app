using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IDownstreamUpdateReconciliationService
{
    Task<DownstreamUpdateReconciliationResponse> ReconcileAsync(
        int maxCount,
        int olderThanMinutes,
        bool includePending,
        string? correlationId,
        CancellationToken ct);
}
