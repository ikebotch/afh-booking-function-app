using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;
using AFH.Booking.Function.Mapping;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class CreateHoldFunction
{
    private readonly ICreateBookingHandler _handler;
    private readonly ILogger<CreateHoldFunction> _logger;

    public CreateHoldFunction(
        ICreateBookingHandler handler,
        ILogger<CreateHoldFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("Bookings_CreateHold")]
    [BookingOpenApiOperation(
        "Bookings",
        "Create hold",
        RequestBodyType = typeof(CreateHoldRequest),
        ResponseType = typeof(CreateBookingResponse),
        SuccessStatusCode = HttpStatusCode.Created)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/hold")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<CreateHoldRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct, Errors.Validation);

        var cmd = body.ToCommand();
        var result = await _handler.HandleAsync(cmd, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.CreatedJsonAsync(result.Value!, ct);
    }
}
