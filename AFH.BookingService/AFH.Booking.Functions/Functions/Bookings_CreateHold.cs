using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Bookings.Handlers;
using AFH.Booking.Contracts.Requests;
using AFH.Booking.Functions.Configuration;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;


namespace AFH.Booking.Functions.Functions;

public sealed class Bookings_CreateHold
{
    private readonly ILogger<Bookings_CreateHold> _logger;
    private readonly ICreateHoldHandler _handler;

    public Bookings_CreateHold(
        ILogger<Bookings_CreateHold> logger,
        ICreateHoldHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    [Function("Bookings_CreateHold")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/hold")] HttpRequestData req,
        CancellationToken ct)
    {
        try
        {
            // Idempotency-Key required
            var idemKey = req.GetHeader("Idempotency-Key");
            if (string.IsNullOrWhiteSpace(idemKey))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "Idempotency-Key header is required.", ct, "BadRequest");

            var body = await req.ReadJsonAsync<CreateHoldRequest>(Json.Options, ct);
            if (body is null)
            {
                var raw = await req.ReadBodyAsStringAsync(ct);
                _logger.LogWarning("Invalid JSON payload for CreateHold. RawBody={RawBody}", raw);
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON payload.", ct, "BadRequest");
            }

            var result = await _handler.HandleAsync(
                new CreateHoldModel(body, idemKey),
                ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_CreateHold.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}
