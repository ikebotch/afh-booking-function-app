using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;
using ContractApprovalRequestResponse = AFH.Booking.Contracts.V1.Responses.ApprovalRequestResponse;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Approvals")]
public sealed class ListAdviserApprovalRequestsFunction
{
    private readonly IApprovalWorkflowService _approvals;

    public ListAdviserApprovalRequestsFunction(IApprovalWorkflowService approvals)
    {
        _approvals = approvals;
    }

    [Function("Approvals_ListAdviserRequests")]
    [BookingOpenApiOperation(
        "Approvals",
        "List adviser booking change requests",
        Description = "Returns approval/change requests scoped to the authenticated adviser. The requester id is resolved from the domain user token, not from query string or request body.",
        ResponseType = typeof(ContractApprovalRequestResponse[]))]
    [BookingOpenApiQueryParameter("bookingId", "string", Description = "Optional booking id filter.", Example = "booking-123")]
    [BookingOpenApiQueryParameter("status", "string", Description = "Optional status filter such as Pending, Approved or Rejected.", Example = "Pending")]
    [BookingOpenApiQueryParameter("changeType", "string", Description = "Optional change type filter: Cancel or Rearrange.", Example = "Rearrange")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/adviser/booking-change-requests")]
        HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var requesterId = ResolveRequesterId(context);
        if (string.IsNullOrWhiteSpace(requesterId))
            return await req.ProblemAsync(HttpStatusCode.Forbidden, "Authenticated adviser identity could not be resolved.", ct, "Forbidden");

        var query = req.Url.Query;
        var requests = await _approvals.ListAsync(new ListApprovalWorkflowRequestsQuery(
            RequesterId: requesterId,
            BookingId: GetQueryValue(query, "bookingId"),
            Status: GetQueryValue(query, "status"),
            ChangeType: GetQueryValue(query, "changeType")), ct);

        return await req.OkJsonAsync(requests.Select(x => x.ToContract()).ToList(), ct);
    }

    private static string? ResolveRequesterId(FunctionContext context)
    {
        var user = context.GetDomainUserContext();
        var principal = context.GetDomainUserPrincipal();
        return user?.UserId
            ?? GetClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier", ClaimTypes.NameIdentifier)
            ?? user?.Email
            ?? GetClaimValue(principal, ClaimTypes.Email, "email", ClaimTypes.Upn, "preferred_username");
    }

    private static string? GetQueryValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var trimmed = query[0] == '?' ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var rawKey = separator >= 0 ? pair[..separator] : pair;
            if (!string.Equals(Uri.UnescapeDataString(rawKey), key, StringComparison.OrdinalIgnoreCase))
                continue;

            var rawValue = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            return Uri.UnescapeDataString(rawValue.Replace("+", "%2B", StringComparison.Ordinal));
        }

        return null;
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
