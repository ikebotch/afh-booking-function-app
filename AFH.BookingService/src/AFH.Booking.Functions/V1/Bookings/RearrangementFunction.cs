using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class RearrangementFunction
{
    private readonly IRearrangementHandler _handler;
    private readonly ILogger<RearrangementFunction> _logger;

    public RearrangementFunction(
        IRearrangementHandler handler,
        ILogger<RearrangementFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("Bookings_RearrangementOptions")]
    public async Task<HttpResponseData> GetOptionsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/rearrangement/options")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

            var body = await req.ReadJsonAsync<GetRearrangementOptionsRequest>(ct) ?? new GetRearrangementOptionsRequest();
            var result = await _handler.GetOptionsAsync(bookingId, body, ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON body in Bookings_RearrangementOptions.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct, "InvalidJson");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_RearrangementOptions.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }

    [Function("Bookings_ExecuteRearrangement")]
    public async Task<HttpResponseData> ExecuteAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/rearrangement/execute")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

            var body = await req.ReadJsonAsync<ExecuteRearrangementRequest>(ct);
            if (body is null)
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

            var result = await _handler.ExecuteAsync(bookingId, body, ct);
            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            if (result.Value?.ApprovalRequired == true && string.Equals(result.Value.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
                return await req.AcceptedJsonAsync(result.Value, ct);

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON body in Bookings_ExecuteRearrangement.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct, "InvalidJson");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_ExecuteRearrangement.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}
