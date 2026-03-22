using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V1.Calendar;

public sealed class ProjectedScheduleFunction
{
    private readonly IAdviserAvailabilityProjectionRepository _projection;

    public ProjectedScheduleFunction(IAdviserAvailabilityProjectionRepository projection)
    {
        _projection = projection;
    }

    [Function("Calendar_ProjectedSchedule")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/calendar/users/{userId}/schedule")]
        HttpRequestData req,
        string userId,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        if (!DateTime.TryParse(query.Get("startUtc"), out var startUtc) ||
            !DateTime.TryParse(query.Get("endUtc"), out var endUtc))
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "startUtc and endUtc are required.", ct, "Validation");
        }

        if (endUtc <= startUtc)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "endUtc must be greater than startUtc.", ct, "Validation");

        startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        endUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);

        var blocks = await _projection.ListBusyBlocksAsync(userId, startUtc, endUtc, ct);
        var bookings = blocks.Select(x => new
        {
            bookingId = x.ProviderEventId,
            subject = x.Subject ?? "Busy",
            startUtc = x.StartUtc,
            endUtc = x.EndUtc,
            status = "Busy"
        }).ToList();

        return await req.OkJsonAsync(new
        {
            userId,
            startUtc,
            endUtc,
            bookings
        }, ct);
    }
}
