using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AFH.Booking.Functions.Functions;

public sealed class Bookings_GetSchedule
{
    private readonly ILogger<Bookings_GetSchedule> _logger;
    private readonly IGetScheduleHandler _handler;

    public Bookings_GetSchedule(
        ILogger<Bookings_GetSchedule> logger,
        IGetScheduleHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    // free/busy blocks
    [Function("Bookings_GetSchedule")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/calendar/advisers/{adviserId}/schedule")] HttpRequestData req,
        string adviserId,
        CancellationToken ct)
    {
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var startUtcRaw = query["startUtc"];
            var endUtcRaw = query["endUtc"];

            if (!DateTime.TryParse(startUtcRaw, out var startUtc) || !DateTime.TryParse(endUtcRaw, out var endUtc))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "startUtc and endUtc query parameters are required.", ct);

            var result = await _handler.HandleAsync(new GetScheduleQuery(adviserId, startUtc, endUtc), ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_GetSchedule.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}
