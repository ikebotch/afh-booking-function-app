using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Functions.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace AFH.Booking.Functions.V1.Calendar;

public sealed class NotificationsFunction
{
    private readonly IProcessNotificationsHandler _handler;
    private readonly ILogger<NotificationsFunction> _logger;

    public NotificationsFunction(
        IProcessNotificationsHandler handler,
        ILogger<NotificationsFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("Calendar_Notifications")]
    public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "v1/calendar/notifications")] HttpRequestData req,
    CancellationToken ct)
    {
        try
        {
            var query = QueryHelpers.ParseQuery(req.Url.Query);

            if (query.TryGetValue("validationToken", out var tokenValues))
            {
                var token = tokenValues.FirstOrDefault();
                var res = req.CreateResponse(HttpStatusCode.OK);
                res.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await res.WriteStringAsync(token ?? string.Empty, ct);
                return res;
            }

            // Normal notifications (calendar-service POSTs JSON)
            if (!req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                return await req.ProblemAsync(
                    HttpStatusCode.MethodNotAllowed,
                    "Only GET(validation) or POST is supported.",
                    ct,
                    "MethodNotAllowed");

            // If body is empty, don’t throw (provider can occasionally send minimal payloads)
            CalendarNotificationsRequest? envelope = null;
            try
            {
                envelope = await req.ReadJsonAsync<CalendarNotificationsRequest>(ct);
            }
            catch
            {
                // swallow: treat as empty notification batch
            }

            var result = await _handler.HandleAsync(envelope, ct);

            // Always ACK notification deliveries
            return await req.AcceptedJsonAsync(new
            {
                accepted = true
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Calendar_Notifications.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}
