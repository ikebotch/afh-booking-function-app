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

public sealed class Bookings_CancelBooking
{
    private readonly ILogger<Bookings_CancelBooking> _logger;
    private readonly ICancelBookingHandler _handler;

    public Bookings_CancelBooking(
        ILogger<Bookings_CancelBooking> logger,
        ICancelBookingHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    [Function("Bookings_CancelBooking")]
    public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/cancel")] HttpRequestData req,
    string bookingId,
    CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "BookingId is required.", ct, "InvalidRequest");

            var body = await req.ReadJsonAsync<CancelBookingRequest>(Json.Options, ct)
                       ?? new CancelBookingRequest(bookingId);

            _logger.LogInformation("Received cancel request for BookingId={BookingId}", bookingId);


            var command = new CancelBookingModel(bookingId, body);
            var result = await _handler.HandleAsync(command, ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Bookings_CancelBooking for BookingId={BookingId}", bookingId);
            return await req.ProblemAsync(HttpStatusCode.InternalServerError, "Something went wrong.", ct, "ServerError");
        }
    }

}
