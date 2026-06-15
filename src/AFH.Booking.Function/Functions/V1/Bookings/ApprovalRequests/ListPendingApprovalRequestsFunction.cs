using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using ContractApprovalRequestResponse = AFH.Booking.Contracts.V1.Responses.ApprovalRequestResponse;

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
    [BookingOpenApiOperation(
        "Approvals",
        "List pending approval requests",
        Description = "Returns pending adviser booking change approval requests for manager/reviewer queues.",
        ResponseType = typeof(ContractApprovalRequestResponse[]))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/approval-requests/pending")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var pending = await _approvals.ListPendingAsync(ct);
        return await req.OkJsonAsync(pending.Select(x => x.ToContract()).ToList(), ct);
    }
}
