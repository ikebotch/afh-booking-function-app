using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;
using ContractApprovalRequestResponse = AFH.Booking.Contracts.V1.Responses.ApprovalRequestResponse;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Approvals")]
public sealed class ReviewApprovalRequestFunction
{
    private readonly IApprovalWorkflowService _approvals;

    public ReviewApprovalRequestFunction(IApprovalWorkflowService approvals)
    {
        _approvals = approvals;
    }

    [Function("Approvals_Review")]
    [BookingOpenApiOperation(
        "Approvals",
        "Review approval request",
        Description = "Approves or rejects an adviser booking change request. The authenticated manager/reviewer domain user is used as the reviewer. If approved, cancellation or rearrangement is executed through the shared booking lifecycle workflow. selectedSlotId is used for rearrangement approvals when the reviewer chooses one proposed option.",
        RequestBodyType = typeof(ReviewApprovalRequest),
        ResponseType = typeof(ContractApprovalRequestResponse),
        RequestExampleJson = """
        {
          "approved": true,
          "notes": "Approved by manager.",
          "selectedSlotId": "slot-456"
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/approval-requests/{requestId}/review")]
        HttpRequestData req,
        FunctionContext context,
        string requestId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "requestId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<ReviewApprovalRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        var actor = BuildReviewerActorContext(context, BookingChangeRequestContext.GetCorrelationId(req));

        var review = await _approvals.ReviewAsync(new ReviewApprovalWorkflowRequest(
            RequestId: requestId.Trim(),
            Approved: body.Approved,
            Reviewer: actor.ActorId ?? actor.DisplayName ?? (string.IsNullOrWhiteSpace(body.Reviewer) ? "Approver" : body.Reviewer.Trim()),
            Notes: body.Notes,
            CorrelationId: actor.CorrelationId,
            ActorContext: actor,
            SelectedSlotId: body.SelectedSlotId), ct);

        if (review is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, $"Approval request '{requestId}' was not found.", ct, "NotFound");

        return await req.OkJsonAsync(review.ToContract(), ct);
    }

    private static BookingActorContext BuildReviewerActorContext(FunctionContext context, string? correlationId)
    {
        var user = context.GetDomainUserContext();
        var principal = context.GetDomainUserPrincipal();
        var actorId = user?.UserId ?? GetClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier", ClaimTypes.NameIdentifier) ?? user?.Email ?? GetClaimValue(principal, ClaimTypes.Email, "email", ClaimTypes.Upn, "preferred_username");
        var displayName = user?.DisplayName ?? GetClaimValue(principal, "name", ClaimTypes.Name);

        return BookingActorContext.ManagerPortal(
            actorId,
            displayName,
            correlationId,
            user?.Permissions);
    }

    private static string? GetClaimValue(ClaimsPrincipal? principal, params string[] claimTypes)
    {
        if (principal is null)
            return null;

        foreach (var claimType in claimTypes)
        {
            var claim = principal.FindFirst(claimType);
            if (!string.IsNullOrWhiteSpace(claim?.Value))
                return claim.Value;
        }

        return null;
    }
}
