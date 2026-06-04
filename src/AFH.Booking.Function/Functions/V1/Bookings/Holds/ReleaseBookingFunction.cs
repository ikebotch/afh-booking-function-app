using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class ReleaseHoldFunction
{
    private readonly IReleaseHoldService _service;
    private readonly ILogger<ReleaseHoldFunction> _logger;

    public ReleaseHoldFunction(
        IReleaseHoldService service,
        ILogger<ReleaseHoldFunction> logger)
    {
        _service = service;
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

        var result = await _service.HandleAsync(new ReleaseHoldCommand
        {
            HoldId = holdId.Trim(),
            ReasonCode = "ManualRelease",
            ReasonDetail = "Released by manual hold release endpoint.",
            ReleaseKind = ReleaseHoldKind.ManualRelease,
            ActorContext = BookingActorContext.InternalAdmin(
                correlationId: BookingChangeRequestContext.GetCorrelationId(req))
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to release hold.",
                ct,
                result.ErrorCode ?? "RELEASE_FAILED");

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
