using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Queries;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class SearchAdminBookingsFunction
{
    private readonly IAdminBookingSearchService _service;

    public SearchAdminBookingsFunction(IAdminBookingSearchService service)
    {
        _service = service;
    }

    [Function("Bookings_AdminSearch")]
    [BookingOpenApiOperation(
        "Bookings",
        "Search admin bookings",
        Description = "Returns paged admin booking results for users with booking admin read permission.",
        ResponseType = typeof(AdminBookingSearchResponse))]
    [BookingOpenApiQueryParameter("search", "string", Description = "Optional free-text search across reference, client, adviser, meeting type and location fields.", Example = "Fiona")]
    [BookingOpenApiQueryParameter("q", "string", Description = "Alias for search.", Example = "BK-123")]
    [BookingOpenApiQueryParameter("bookingId", "string", Description = "Optional booking id filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "booking-123")]
    [BookingOpenApiQueryParameter("bookingReference", "string", Description = "Optional booking reference filter. reference is accepted as an alias. Repeat the parameter or use comma-separated values for multiple selections.", Example = "BK-123")]
    [BookingOpenApiQueryParameter("transactionId", "string", Description = "Optional booking transaction id filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "tx-123")]
    [BookingOpenApiQueryParameter("transactionRef", "string", Description = "Optional external transaction/client reference filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "TRX-123")]
    [BookingOpenApiQueryParameter("status", "string", Description = "Optional hold status filter: Active, Confirmed, PendingReschedule, Released, Cancelled or Expired. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Confirmed,PendingReschedule")]
    [BookingOpenApiQueryParameter("adviserId", "string", Description = "Optional adviser id filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "adv-123")]
    [BookingOpenApiQueryParameter("adviserName", "string", Description = "Optional adviser name filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "John Doe")]
    [BookingOpenApiQueryParameter("clientRef", "string", Description = "Optional client/user reference filter. clientId is accepted as an alias. Repeat the parameter or use comma-separated values for multiple selections.", Example = "client-123")]
    [BookingOpenApiQueryParameter("leadName", "string", Description = "Optional lead/client name filter. clientName is accepted as an alias. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Fiona")]
    [BookingOpenApiQueryParameter("locationRef", "string", Description = "Optional location reference filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "branch-123")]
    [BookingOpenApiQueryParameter("meetingType", "string", Description = "Optional meeting type filter. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Review")]
    [BookingOpenApiQueryParameter("meetingTopic", "string", Description = "Alias for meetingType. Repeat the parameter or use comma-separated values for multiple selections.", Example = "Pensions")]
    [BookingOpenApiQueryParameter("mode", "string", Description = "Optional mode filter: " + BookingModeScalars.Online + ", " + BookingModeScalars.InPerson + " or " + BookingModeScalars.Phone + ". Repeat the parameter or use comma-separated values for multiple selections.", Example = BookingModeScalars.InPerson)]
    [BookingOpenApiQueryParameter("from", "string", Format = "date-time", Description = "Optional UTC lower bound for booking start.", Example = "2026-07-01T00:00:00Z")]
    [BookingOpenApiQueryParameter("to", "string", Format = "date-time", Description = "Optional UTC upper bound for booking start.", Example = "2026-07-31T23:59:59Z")]
    [BookingOpenApiQueryParameter("dateFrom", "string", Format = "date-time", Description = "Alias for from.", Example = "2026-07-01T00:00:00Z")]
    [BookingOpenApiQueryParameter("dateTo", "string", Format = "date-time", Description = "Alias for to.", Example = "2026-07-31T23:59:59Z")]
    [BookingOpenApiQueryParameter("page", "integer", Description = "1-based page number.", Example = "1")]
    [BookingOpenApiQueryParameter("pageSize", "integer", Description = "Page size from 1 to 100.", Example = "25")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/bookings")] HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var authResult = await BookingFunctionActorContext.BuildAuthenticatedAsync(req, context, ct);
        if (!authResult.IsSuccess)
            return authResult.Response!;

        var scope = BuildBookingSearchScope(authResult.User!);
        var fromIsValid = TryParseUtc(QueryFirst(req, "from", "dateFrom", "startFrom", "startUtcFrom"), out var fromUtc, out var fromError);
        var toIsValid = TryParseUtc(QueryFirst(req, "to", "dateTo", "startTo", "startUtcTo"), out var toUtc, out var toError);

        if (!fromIsValid || !toIsValid)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, fromError ?? toError ?? "Invalid date filter.", ct, Errors.Validation);
        }

        var query = BuildQuery(req, scope, fromUtc, toUtc);

        var result = await _service.SearchAsync(query, ct);
        if (!result.IsSuccess)
        {
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);
        }

        var response = result.Value!;

        return await req.OkJsonAsync(
            response.ToContract(),
            ct,
            new ApiPaging
            {
                Page = response.Page,
                PageSize = response.PageSize,
                TotalItems = response.TotalItems,
                TotalPages = response.TotalPages
            });
    }

    private static SearchAdminBookingsQuery BuildQuery(
        HttpRequestData req,
        BookingSearchAccessScope scope,
        DateTime? fromUtc,
        DateTime? toUtc)
        => new()
        {
            Search = QueryFirst(req, "search", "q", "query", "keyword"),
            BookingIds = req.QueryMany("bookingId"),
            BookingReferences = QueryMany(req, "bookingReference", "bookingRef", "reference"),
            TransactionIds = req.QueryMany("transactionId"),
            TransactionRefs = QueryMany(req, "transactionRef", "transactionReference"),
            Statuses = req.QueryMany("status"),
            AdviserIds = req.QueryMany("adviserId"),
            AdviserNames = req.QueryMany("adviserName"),
            ClientRefs = QueryMany(req, "clientRef", "clientId"),
            ClientNames = QueryMany(req, "leadName", "clientName", "customerName"),
            LocationRefs = req.QueryMany("locationRef"),
            MeetingTypes = QueryMany(req, "meetingType", "meetingTopic", "topic"),
            Modes = req.QueryMany("mode"),
            HasUnrestrictedAccess = scope.HasUnrestrictedAccess,
            ScopedAdviserIds = scope.ScopedAdviserIds,
            ScopedRegions = scope.ScopedRegions,
            ScopedLocationRefs = scope.ScopedLocationRefs,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = ParseInt(req.Query("page"), 1),
            PageSize = ParseInt(req.Query("pageSize"), 25)
        };

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

    private static string? QueryFirst(HttpRequestData req, params string[] keys)
        => keys.Select(key => req.Query(key)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static IReadOnlyList<string> QueryMany(HttpRequestData req, params string[] keys)
        => keys.SelectMany(key => req.QueryMany(key)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static BookingSearchAccessScope BuildBookingSearchScope(AFH.Booking.Application.Models.Auth.AdviserUserContext user)
    {
        if (BookingFunctionActorContext.HasUnrestrictedScope(user, "Bookings"))
            return new BookingSearchAccessScope(true, [], [], []);

        var adviserIds = new List<string>();
        var regions = new List<string>();
        var locationRefs = new List<string>();

        if (!string.IsNullOrWhiteSpace(user.AdviserId))
            adviserIds.Add(user.AdviserId);

        foreach (var scope in user.AccessScopes.Where(scope =>
                     string.Equals(scope.Area, "Bookings", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(scope.Area, "*", StringComparison.OrdinalIgnoreCase)))
        {
            if (scope.ScopeType.Equals("AdviserSelf", StringComparison.OrdinalIgnoreCase)
                || scope.ScopeType.Equals("Adviser", StringComparison.OrdinalIgnoreCase))
            {
                AddIfPresent(adviserIds, scope.ScopeValue);
            }
            else if (scope.ScopeType.Equals("Region", StringComparison.OrdinalIgnoreCase))
            {
                AddIfPresent(regions, scope.ScopeValue);
            }
            else if (scope.ScopeType.Equals("Branch", StringComparison.OrdinalIgnoreCase)
                     || scope.ScopeType.Equals("Location", StringComparison.OrdinalIgnoreCase))
            {
                AddIfPresent(locationRefs, scope.ScopeValue);
            }
        }

        return new BookingSearchAccessScope(
            false,
            adviserIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            regions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            locationRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void AddIfPresent(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value.Trim());
    }

    private sealed record BookingSearchAccessScope(
        bool HasUnrestrictedAccess,
        IReadOnlyList<string> ScopedAdviserIds,
        IReadOnlyList<string> ScopedRegions,
        IReadOnlyList<string> ScopedLocationRefs);

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
}
