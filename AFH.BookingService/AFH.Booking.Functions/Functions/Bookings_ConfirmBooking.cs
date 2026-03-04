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

public sealed class Bookings_ConfirmBooking
{
    private readonly ILogger<Bookings_ConfirmBooking> _logger;
    private readonly IConfirmBookingHandler _handler;

    public Bookings_ConfirmBooking(
        ILogger<Bookings_ConfirmBooking> logger,
        IConfirmBookingHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    [Function("Bookings_ConfirmBooking")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/confirm")] HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        try
        {
            var body = await req.ReadJsonAsync<ConfirmBookingRequest>(Json.Options, ct) ?? new ConfirmBookingRequest(bookingId);

            var result = await _handler.HandleAsync(
                new ConfirmBookingModel(bookingId, body),
                ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_ConfirmBooking.");
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }
}
