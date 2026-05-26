using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class SelfServiceGetBookingDetailsFunction
{
    private readonly IBookingChangeAccessService _accessService;
    private readonly IBookingDetailsService _service;

    public SelfServiceGetBookingDetailsFunction(
        IBookingChangeAccessService accessService,
        IBookingDetailsService service)
    {
        _accessService = accessService;
        _service = service;
    }

    [Function("Bookings_SelfServiceGetBooking")]
    [BookingOpenApiOperation(
        "Bookings",
        "Get self-service booking details",
        ResponseType = typeof(BookingDetailsResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/self-service/bookings/{bookingId}")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var access = await BookingChangeRequestContext.ValidateClientAsync(req, bookingId, _accessService, ct);
        if (!access.IsSuccess)
            return await req.ProblemAsync(access.StatusCode, access.ErrorMessage ?? "Unauthorized.", ct, access.ErrorCode);

        var result = await _service.HandleAsync(new GetBookingDetailsQuery { BookingId = bookingId.Trim() }, ct);
        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
