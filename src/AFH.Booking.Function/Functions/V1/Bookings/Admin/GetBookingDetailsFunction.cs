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
    private readonly IBookingDetailsService _service;

    public GetBookingDetailsFunction(IBookingDetailsService service)
    {
        _service = service;
    }

    [Function("Bookings_GetBooking")]
    [BookingOpenApiOperation(
        "Bookings",
        "Get booking details",
        ResponseType = typeof(BookingDetailsResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/bookings/{bookingId}")] HttpRequestData req,
        FunctionContext context,
        string bookingId,
        CancellationToken ct)
    {
        var authResult = await BookingFunctionActorContext.BuildAuthenticatedAsync(req, context, ct);
        if (!authResult.IsSuccess)
            return authResult.Response!;

        var result = await _service.HandleAsync(new GetBookingDetailsQuery { BookingId = bookingId }, ct);

        if (!result.IsSuccess)
        {
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);
        }

        var forbidden = await BookingFunctionActorContext.EnsureCanAccessBookingAsync(req, authResult.User!, result.Value!, ct);
        if (forbidden is not null)
            return forbidden;

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
