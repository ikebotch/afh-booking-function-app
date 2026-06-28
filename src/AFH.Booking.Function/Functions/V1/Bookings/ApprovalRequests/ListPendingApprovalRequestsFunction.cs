using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using ContractApprovalRequestResponse = AFH.Booking.Contracts.V1.Responses.ApprovalRequestResponse;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Approvals")]
public sealed class ListPendingApprovalRequestsFunction
{
    private readonly IApprovalWorkflowService _approvals;
    private readonly IBookingDetailsService _details;

    public ListPendingApprovalRequestsFunction(
        IApprovalWorkflowService approvals,
        IBookingDetailsService details)
    {
        _approvals = approvals;
        _details = details;
    }

    [Function("Approvals_ListPending")]
    [BookingOpenApiOperation(
        "Approvals",
        "List pending approval requests",
        Description = "Returns pending adviser booking change approval requests for manager/reviewer queues.",
        ResponseType = typeof(ContractApprovalRequestResponse[]))]
    [BookingOpenApiQueryParameter("page", "integer", Description = "1-based page number.", Example = "1")]
    [BookingOpenApiQueryParameter("pageSize", "integer", Description = "Page size from 1 to 100.", Example = "25")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/approval-requests/pending")]
        HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var authResult = await BookingFunctionActorContext.BuildAuthenticatedAsync(req, context, ct);
        if (!authResult.IsSuccess)
            return authResult.Response!;

        var pending = await _approvals.ListPendingAsync(ct);
        var scoped = await FilterByAccessScopeAsync(authResult.User!, pending, ct);
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
