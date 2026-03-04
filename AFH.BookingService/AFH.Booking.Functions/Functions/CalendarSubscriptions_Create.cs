using AFH.Booking.Application.Calendar.Subscriptions;
using AFH.Booking.Contracts.Requests;
using AFH.Booking.Functions.Configuration;
using AFH.Booking.Functions.Http;
using AFH.Common.CalendarUtils.Sdk.Contracts.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Functions.Functions;

public sealed class CalendarSubscriptions_Create
{
    private readonly ILogger<CalendarSubscriptions_Create> _logger;
    private readonly ICalendarSubscriptionService _svc;

    public CalendarSubscriptions_Create(ILogger<CalendarSubscriptions_Create> logger, ICalendarSubscriptionService svc)
    {
        _logger = logger;
        _svc = svc;
    }

    [Function("Calendar_Subscriptions_Create")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/calendar/subscriptions")]
        HttpRequestData req,
        CancellationToken ct)
    {
        CreateCalendarSubscriptionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateCalendarSubscriptionRequest>(req.Body, Json.Options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid request payload.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid payload.", ct);
        }

        var adviserId = body?.AdviserId?.Trim();
        if (string.IsNullOrWhiteSpace(adviserId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "adviserId is required.", ct);

        var result = await _svc.EnsureAsync(adviserId, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!, ct);
    }
}