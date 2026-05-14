using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class GetBookingDetailsFunction
{
    private readonly IBookingDetailsHandler _handler;

    public GetBookingDetailsFunction(IBookingDetailsHandler handler)
    {
        _handler = handler;
    }

    [Function("Bookings_GetBooking")]
    [BookingOpenApiOperation(
        "Bookings",
        "Get booking details",
        ResponseType = typeof(BookingDetailsResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/bookings/{bookingId}")] HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var result = await _handler.HandleAsync(new GetBookingDetailsQuery { BookingId = bookingId }, ct);

        if (!result.IsSuccess)
        {
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);
        }

        return await req.OkJsonAsync(result.Value!, ct);
    }
}
