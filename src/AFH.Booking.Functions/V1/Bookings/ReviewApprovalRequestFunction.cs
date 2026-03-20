using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class ReviewApprovalRequestFunction
{
    private readonly IApprovalWorkflowService _approvals;

    public ReviewApprovalRequestFunction(IApprovalWorkflowService approvals)
    {
        _approvals = approvals;
    }

    [Function("Approvals_Review")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/approval-requests/{requestId}/review")]
        HttpRequestData req,
        string requestId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "requestId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<ReviewApprovalRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        var review = await _approvals.ReviewAsync(
            requestId.Trim(),
            approved: body.Approved,
            reviewer: string.IsNullOrWhiteSpace(body.Reviewer) ? "Ian" : body.Reviewer.Trim(),
            notes: body.Notes,
            ct: ct);

        if (review is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, $"Approval request '{requestId}' was not found.", ct, "NotFound");

        return await req.OkJsonAsync(review, ct);
    }
}
