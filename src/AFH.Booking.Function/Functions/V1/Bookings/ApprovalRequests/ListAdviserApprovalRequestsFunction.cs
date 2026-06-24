using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
    [BookingOpenApiQueryParameter("page", "integer", Description = "1-based page number.", Example = "1")]
    [BookingOpenApiQueryParameter("pageSize", "integer", Description = "Page size from 1 to 100.", Example = "25")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/adviser/booking-change-requests")]
        HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var requesterId = ResolveRequesterId(context);
        if (string.IsNullOrWhiteSpace(requesterId))
            return await req.ProblemAsync(HttpStatusCode.Forbidden, "Signed-in user is not mapped to an adviser profile.", ct, "Forbidden");

        var query = req.Url.Query;
        var requests = await _approvals.ListAsync(new ListApprovalWorkflowRequestsQuery(
            RequesterId: requesterId,
            BookingId: GetQueryValue(query, "bookingId"),
            Status: GetQueryValue(query, "status"),
            ChangeType: GetQueryValue(query, "changeType")), ct);

        var paged = ApplyPaging(requests, req);

        return await req.OkJsonAsync(
            paged.Items.Select(x => x.ToContract()).ToList(),
            ct,
            paged.Paging);
    }

    private static string? ResolveRequesterId(FunctionContext context)
    {
        var user = context.GetDomainUserContext();
        return user?.AdviserId;
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

    private static PagedItems<T> ApplyPaging<T>(IReadOnlyList<T> items, HttpRequestData req)
    {
        var page = Math.Max(ParseInt(req.Query("page"), 1), 1);
        var pageSize = Math.Clamp(ParseInt(req.Query("pageSize"), 25), 1, 100);
        var totalItems = items.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedItems<T>(
            pageItems,
            new ApiPaging
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            });
    }

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

    private sealed record PagedItems<T>(IReadOnlyList<T> Items, ApiPaging Paging);
}
