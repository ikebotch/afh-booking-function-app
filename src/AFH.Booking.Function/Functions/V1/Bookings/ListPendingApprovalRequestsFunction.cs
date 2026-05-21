using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Approvals")]
public sealed class ListPendingApprovalRequestsFunction
{
    private readonly IApprovalWorkflowService _approvals;

    public ListPendingApprovalRequestsFunction(IApprovalWorkflowService approvals)
    {
        _approvals = approvals;
    }

    [Function("Approvals_ListPending")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/approval-requests/pending")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var pending = await _approvals.ListPendingAsync(ct);
        return await req.OkJsonAsync(pending.Select(x => x.ToContract()).ToList(), ct);
    }
}
