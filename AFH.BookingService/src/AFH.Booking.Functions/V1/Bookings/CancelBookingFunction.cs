using System.Net;
using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class CancelBookingFunction
{
    private readonly ICancelBookingHandler _handler;
    private readonly ILogger<CancelBookingFunction> _logger;

    public CancelBookingFunction(
        ICancelBookingHandler handler,
        ILogger<CancelBookingFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("Bookings_CancelBooking")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/cancel")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

            var body = await req.ReadJsonAsync<CancelBookingRequest>(ct);

            var cmd = new CancelBookingCommand
            {
                BookingId = bookingId.Trim(),
                Reason = body?.Reason
            };

            var result = await _handler.HandleAsync(cmd, ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(
                    result.StatusCode,
                    result.ErrorMessage ?? "Request failed.",
                    ct,
                    result.ErrorCode);

            // If your handler returns a payload (recommended)
            if (result.Value is not null)
                return await req.OkJsonAsync(result.Value, ct);

            // If handler returns success without data
            return await req.OkJsonAsync(new { message = "Booking cancelled." }, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON body in Bookings_CancelBooking.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct, "InvalidJson");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_CancelBooking.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}