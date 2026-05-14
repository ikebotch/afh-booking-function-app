using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
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
    [BookingOpenApiOperation(
        "Bookings",
        "Release hold",
        ResponseType = typeof(ReleaseHoldResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "v1/bookings/holds/{holdId}/release")]
        HttpRequestData req,
        string holdId,
        CancellationToken ct)
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

        return await req.OkJsonAsync(result.Value!, ct);
    }
}
