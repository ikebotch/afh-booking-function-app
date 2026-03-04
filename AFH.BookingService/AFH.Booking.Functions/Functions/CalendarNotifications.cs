using AFH.Booking.Application.Calendar.Notifications;
using AFH.Booking.Contracts.Webhooks;
using AFH.Booking.Functions.Configuration;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Functions.Functions;

public sealed class CalendarNotifications
{
    private readonly ILogger<CalendarNotifications> _logger;
    private readonly ICalendarNotificationHandler _handler;

    public CalendarNotifications(
        ILogger<CalendarNotifications> logger,
        ICalendarNotificationHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    [Function("Calendar_Notifications")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "v1/calendar/notifications")]
        HttpRequestData req,
        CancellationToken ct)
    {
        // Graph validation handshake
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var validationToken = query["validationToken"];
        if (!string.IsNullOrWhiteSpace(validationToken))
        {
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteStringAsync(validationToken, ct);
            return ok;
        }

        if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) is false)
            return await req.ProblemAsync(HttpStatusCode.MethodNotAllowed, "Only POST/GET supported.", ct);

        GraphNotificationEnvelope? envelope;
        try
        {
            envelope = await JsonSerializer.DeserializeAsync<GraphNotificationEnvelope>(req.Body, Json.Options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Graph notification payload.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid notification payload.", ct);
        }

        var result = await _handler.HandleAsync(envelope, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Notification failed.", ct, result.ErrorCode);

        // MUST respond quickly; do heavy work async behind the scenes if needed
        return req.CreateResponse(HttpStatusCode.Accepted);
    }
}