using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Internal/Admin")]
public sealed class RemediateBookingShowAsFunction
{
    private readonly IBookingShowAsRemediationService _service;

    public RemediateBookingShowAsFunction(IBookingShowAsRemediationService service)
    {
        _service = service;
    }

    [Function("Bookings_RemediateShowAs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/calendar/remediate-showas")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var result = await _service.HandleAsync(bookingId, ct);
        if (!result.IsSuccess)
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);

        return await req.OkJsonAsync(result.Value!, ct);
    }
}
