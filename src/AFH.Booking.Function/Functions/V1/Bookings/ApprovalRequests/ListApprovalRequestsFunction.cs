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
    [BookingOpenApiQueryParameter("search", "string", Description = "Optional free-text search across approval reference, booking reference, client/lead, adviser, meeting topic and change type.", Example = "BK-123")]
    [BookingOpenApiQueryParameter("q", "string", Description = "Alias for search.", Example = "Fiona")]
    [BookingOpenApiQueryParameter("bookingId", "string", Description = "Optional booking id filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "booking-123")]
    [BookingOpenApiQueryParameter("bookingReference", "string", Description = "Optional booking reference filter. reference is accepted as an alias. Repeat the parameter or use comma-separated values for multiple selections.", Example = "BK-123")]
    [BookingOpenApiQueryParameter("status", "string", Description = "Optional status filter such as Pending, Approved or Rejected. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Pending,Approved,Rejected")]
    [BookingOpenApiQueryParameter("changeType", "string", Description = "Optional change type filter: Cancel or Rearrange. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Rearrange,Cancel")]
    [BookingOpenApiQueryParameter("adviserId", "string", Description = "Optional adviser id filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "adv-123")]
    [BookingOpenApiQueryParameter("adviserName", "string", Description = "Optional adviser name filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "John Doe")]
    [BookingOpenApiQueryParameter("leadName", "string", Description = "Optional lead/client name filter. clientName is accepted as an alias. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Fiona")]
    [BookingOpenApiQueryParameter("meetingTopic", "string", Description = "Optional meeting topic filter. meetingType is accepted as an alias. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Pensions")]
    [BookingOpenApiQueryParameter("requestedBy", "string", Description = "Optional requested-by actor filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Adviser")]
    [BookingOpenApiQueryParameter("from", "string", Format = "date-time", Description = "Optional UTC lower bound. Defaults to booking date unless dateField=requested.", Example = "2026-07-01T00:00:00Z")]
    [BookingOpenApiQueryParameter("to", "string", Format = "date-time", Description = "Optional UTC upper bound. Defaults to booking date unless dateField=requested.", Example = "2026-07-31T23:59:59Z")]
    [BookingOpenApiQueryParameter("dateFrom", "string", Format = "date-time", Description = "Alias for from.", Example = "2026-07-01T00:00:00Z")]
    [BookingOpenApiQueryParameter("dateTo", "string", Format = "date-time", Description = "Alias for to.", Example = "2026-07-31T23:59:59Z")]
    [BookingOpenApiQueryParameter("dateField", "string", Description = "Date field to filter: booking or requested.", Example = "booking")]
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

        var fromIsValid = TryParseUtc(QueryFirst(req, "from", "dateFrom", "startFrom", "requestedFrom"), out var fromUtc, out var fromError);
        var toIsValid = TryParseUtc(QueryFirst(req, "to", "dateTo", "startTo", "requestedTo"), out var toUtc, out var toError);

        if (!fromIsValid || !toIsValid)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, fromError ?? toError ?? "Invalid date filter.", ct, Errors.Validation);
        }

        var requests = await _approvals.ListAsync(new ListApprovalWorkflowRequestsQuery(
            RequesterId: null,
            BookingIds: req.QueryMany("bookingId"),
            Statuses: req.QueryMany("status"),
            ChangeTypes: req.QueryMany("changeType"),
            Search: QueryFirst(req, "search", "q", "query", "keyword"),
            BookingReferences: QueryMany(req, "bookingReference", "bookingRef", "reference"),
            TransactionIds: req.QueryMany("transactionId"),
            TransactionRefs: QueryMany(req, "transactionRef", "transactionReference"),
            AdviserIds: req.QueryMany("adviserId"),
            AdviserNames: req.QueryMany("adviserName"),
            ClientNames: QueryMany(req, "leadName", "clientName", "customerName"),
            MeetingTypes: QueryMany(req, "meetingTopic", "meetingType", "topic"),
            RequestedBys: req.QueryMany("requestedBy"),
            FromUtc: fromUtc,
            ToUtc: toUtc,
            DateField: req.Query("dateField")), ct);

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

    private static string? QueryFirst(HttpRequestData req, params string[] keys)
        => keys.Select(key => req.Query(key)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static IReadOnlyList<string> QueryMany(HttpRequestData req, params string[] keys)
        => keys.SelectMany(key => req.QueryMany(key)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool TryParseUtc(string? value, out DateTime? parsed, out string? error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!DateTimeOffset.TryParse(value, out var dto))
        {
            error = $"'{value}' is not a valid UTC date/time.";
            return false;
        }

        parsed = dto.UtcDateTime;
        return true;
    }

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
