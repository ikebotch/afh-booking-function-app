using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V1.Calendar;

public sealed class CreateSubscriptionFunction
{
    private readonly ICreateSubscriptionHandler _handler;
    private readonly ILogger<CreateSubscriptionFunction> _logger;

    public CreateSubscriptionFunction(
        ICreateSubscriptionHandler handler,
        ILogger<CreateSubscriptionFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("Calendar_Subscriptions_Create")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/calendar/subscriptions")] HttpRequestData req,
        CancellationToken ct)
    {
        try
        {
            var cmd = await req.ReadJsonAsync<CreateCalendarSubscriptionRequest>(ct);
            if (cmd is null)
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct);

            var result = await _handler.HandleAsync(cmd, ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            return await req.CreatedJsonAsync(result.Value!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Calendar_Subscriptions_Create.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}
