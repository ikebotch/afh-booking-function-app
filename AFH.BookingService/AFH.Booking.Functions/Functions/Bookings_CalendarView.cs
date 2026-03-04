using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Functions.Http;
using AFH.Booking.Functions.Mapping;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AFH.Booking.Functions.Functions;

public sealed class Bookings_CalendarView
{
    private readonly ILogger<Bookings_CalendarView> _logger;
    private readonly ICalendarViewHandler _handler;

    public Bookings_CalendarView(
        ILogger<Bookings_CalendarView> logger,
        ICalendarViewHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    // list events + conflict detection
    [Function("Bookings_CalendarView")]
    //public async Task<HttpResponseData> Run(
    //    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/calendar/advisers/{adviserId}/view")] HttpRequestData req,
    //    string adviserId,
    //    CancellationToken ct)
    //{
    public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/calendar/view")]
    HttpRequestData req,
    CancellationToken ct)
    {
        try
        {
            if (!CalendarViewQueryMapper.TryMap(
                    req,
                    out var query,
                    out var status,
                    out var error))
            {
                return await req.ProblemAsync(status, error, ct);
            }

            var result = await _handler.HandleAsync(query, ct);

            if (!result.IsSuccess)
            {
                return await req.ProblemAsync(
                    result.StatusCode,
                    result.ErrorMessage ?? "Request failed.",
                    ct,
                    result.ErrorCode);
            }

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_CalendarView");

            return await req.ProblemAsync(
                HttpStatusCode.InternalServerError,
                "Something went wrong.",
                ct,
                "ServerError");
        }
    }
}
