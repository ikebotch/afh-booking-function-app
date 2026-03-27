using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V1.Admin;

[BookingOpenApiTag("Internal/Admin")]
public sealed class ReconcileDownstreamUpdatesFunction
{
    private readonly IDownstreamUpdateReconciliationService _reconciliation;
    private readonly ILogger<ReconcileDownstreamUpdatesFunction> _logger;

    public ReconcileDownstreamUpdatesFunction(
        IDownstreamUpdateReconciliationService reconciliation,
        ILogger<ReconcileDownstreamUpdatesFunction> logger)
    {
        _reconciliation = reconciliation;
        _logger = logger;
    }

    [Function("Admin_ReconcileDownstreamUpdates")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/downstream-updates/reconcile")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<DownstreamUpdateReconciliationRequest>(ct);
        var correlationId = req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault()
            : null;

        var result = await _reconciliation.ReconcileAsync(
            maxCount: Math.Clamp(payload?.MaxCount ?? 25, 1, 100),
            olderThanMinutes: Math.Clamp(payload?.OlderThanMinutes ?? 5, 0, 24 * 60),
            includePending: payload?.IncludePending ?? true,
            correlationId: correlationId,
            ct: ct);

        _logger.LogInformation(
            "Downstream reconciliation completed. Requested={RequestedCount} Retried={RetriedCount} Succeeded={SucceededCount} Failed={FailedCount} CorrelationId={CorrelationId}",
            result.RequestedCount,
            result.RetriedCount,
            result.SucceededCount,
            result.FailedCount,
            correlationId);

        return await req.OkJsonAsync(result, ct);
    }
}
