using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using ContractApprovalRequestResponse = AFH.Booking.Contracts.V1.Responses.ApprovalRequestResponse;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Approvals")]
public sealed class ListApprovalRequestsFunction
{
    private readonly IApprovalWorkflowService _approvals;
    private readonly IBookingDetailsService _details;

    public ListApprovalRequestsFunction(
        IApprovalWorkflowService approvals,
        IBookingDetailsService details)
    {
        _approvals = approvals;
        _details = details;
    }

    [Function("Approvals_List")]
    [BookingOpenApiOperation(
        "Approvals",
        "List approval requests",
        Description = "Returns adviser booking change approval requests visible to the signed-in manager/reviewer. Use status, bookingId, and changeType query parameters to filter.",
        ResponseType = typeof(ContractApprovalRequestResponse[]))]
    [BookingOpenApiQueryParameter("bookingId", "string", Description = "Optional booking id filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "booking-123")]
    [BookingOpenApiQueryParameter("status", "string", Description = "Optional status filter such as Pending, Approved or Rejected. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Pending,Approved,Rejected")]
    [BookingOpenApiQueryParameter("changeType", "string", Description = "Optional change type filter: Cancel or Rearrange. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Rearrange,Cancel")]
    [BookingOpenApiQueryParameter("page", "integer", Description = "1-based page number.", Example = "1")]
    [BookingOpenApiQueryParameter("pageSize", "integer", Description = "Page size from 1 to 100.", Example = "25")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/approval-requests")]
        HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var authResult = await BookingFunctionActorContext.BuildAuthenticatedAsync(req, context, ct);
        if (!authResult.IsSuccess)
            return authResult.Response!;

        var requests = await _approvals.ListAsync(new ListApprovalWorkflowRequestsQuery(
            RequesterId: null,
            BookingIds: req.QueryMany("bookingId"),
            Statuses: req.QueryMany("status"),
            ChangeTypes: req.QueryMany("changeType")), ct);

        var scoped = await FilterByAccessScopeAsync(authResult.User!, requests, ct);
        var paged = ApplyPaging(scoped, req);

        return await req.OkJsonAsync(
            paged.Items.Select(x => x.ToContract()).ToList(),
            ct,
            paged.Paging);
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

    private async Task<IReadOnlyList<AFH.Booking.Application.Models.Approvals.ApprovalRequestResponse>> FilterByAccessScopeAsync(
        AFH.Booking.Application.Models.Auth.AdviserUserContext user,
        IReadOnlyList<AFH.Booking.Application.Models.Approvals.ApprovalRequestResponse> requests,
        CancellationToken ct)
    {
        if (BookingFunctionActorContext.HasUnrestrictedScope(user, "Bookings"))
            return requests;

        var allowed = new List<AFH.Booking.Application.Models.Approvals.ApprovalRequestResponse>();
        foreach (var request in requests)
        {
            var details = await _details.HandleAsync(new GetBookingDetailsQuery { BookingId = request.BookingId }, ct);
            if (details.IsSuccess && details.Value is not null && BookingFunctionActorContext.CanAccessBooking(user, details.Value))
                allowed.Add(request);
        }

        return allowed;
    }

    private sealed record PagedItems<T>(IReadOnlyList<T> Items, ApiPaging Paging);
}
