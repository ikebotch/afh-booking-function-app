using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class ReleaseHoldFunction
{
    private readonly IReleaseHoldHandler _handler;
    private readonly ILogger<ReleaseHoldFunction> _logger;

    public ReleaseHoldFunction(
        IReleaseHoldHandler handler,
        ILogger<ReleaseHoldFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("Bookings_ReleaseHold")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "v1/bookings/holds/{holdId}/release")]
        HttpRequestData req,
        string holdId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(holdId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "holdId is required.", ct, "Validation");

            var result = await _handler.HandleAsync(holdId.Trim(), ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(
                    result.StatusCode,
                    result.ErrorMessage ?? "Unable to release hold.",
                    ct,
                    result.ErrorCode ?? "RELEASE_FAILED");

            // Success JSON payload
            return await req.OkJsonAsync(
                    result.Value!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_ReleaseHold. HoldId={HoldId}", holdId);
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}