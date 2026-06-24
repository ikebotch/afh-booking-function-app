using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
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
    [BookingOpenApiQueryParameter("bookingId", "string", Description = "Optional booking id filter.", Example = "booking-123")]
    [BookingOpenApiQueryParameter("transactionId", "string", Description = "Optional booking transaction id filter.", Example = "tx-123")]
    [BookingOpenApiQueryParameter("transactionRef", "string", Description = "Optional external transaction/client reference filter.", Example = "TRX-123")]
    [BookingOpenApiQueryParameter("status", "string", Description = "Optional hold status filter: Active, Confirmed, Released, Cancelled or Expired.", Example = "Confirmed")]
    [BookingOpenApiQueryParameter("adviserId", "string", Description = "Optional adviser id filter.", Example = "adv-123")]
    [BookingOpenApiQueryParameter("clientRef", "string", Description = "Optional client/user reference filter. clientId is accepted as an alias.", Example = "client-123")]
    [BookingOpenApiQueryParameter("locationRef", "string", Description = "Optional location reference filter.", Example = "branch-123")]
    [BookingOpenApiQueryParameter("meetingType", "string", Description = "Optional meeting type filter.", Example = "Review")]
    [BookingOpenApiQueryParameter("from", "string", Format = "date-time", Description = "Optional UTC lower bound for booking start.", Example = "2026-07-01T00:00:00Z")]
    [BookingOpenApiQueryParameter("to", "string", Format = "date-time", Description = "Optional UTC upper bound for booking start.", Example = "2026-07-31T23:59:59Z")]
    [BookingOpenApiQueryParameter("page", "integer", Description = "1-based page number.", Example = "1")]
    [BookingOpenApiQueryParameter("pageSize", "integer", Description = "Page size from 1 to 100.", Example = "25")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/bookings")] HttpRequestData req,
        CancellationToken ct)
    {
        var fromIsValid = TryParseUtc(req.Query("from"), out var fromUtc, out var fromError);
        var toIsValid = TryParseUtc(req.Query("to"), out var toUtc, out var toError);

        if (!fromIsValid || !toIsValid)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, fromError ?? toError ?? "Invalid date filter.", ct, Errors.Validation);
        }

        var query = new SearchAdminBookingsQuery
        {
            BookingId = req.Query("bookingId"),
            TransactionId = req.Query("transactionId"),
            TransactionRef = req.Query("transactionRef"),
            Status = req.Query("status"),
            AdviserId = req.Query("adviserId"),
            ClientRef = req.Query("clientRef") ?? req.Query("clientId"),
            LocationRef = req.Query("locationRef"),
            MeetingType = req.Query("meetingType"),
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = ParseInt(req.Query("page"), 1),
            PageSize = ParseInt(req.Query("pageSize"), 25)
        };

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

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

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
