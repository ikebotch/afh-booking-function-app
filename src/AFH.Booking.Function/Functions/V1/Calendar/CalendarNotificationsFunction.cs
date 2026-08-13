using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Models.Calendar;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Calendar;

[BookingOpenApiTag("Internal/Calendar")]
public sealed class CalendarNotificationsFunction
{
    private readonly IBookingShowAsRemediationService _service;

    public CalendarNotificationsFunction(IBookingShowAsRemediationService service)
    {
        _service = service;
    }

    [Function("Bookings_CalendarNotifications")]
    [BookingOpenApiOperation(
        "Internal/Calendar",
        "Process calendar provider notifications",
        Description = "Internal endpoint used by Calendar Service to report Outlook provider-side edits and deletions for booking-managed events.",
        ResponseType = typeof(CalendarProviderNotificationProcessingResult))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/calendar/notifications")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadFromJsonAsync<CalendarProviderNotificationEnvelope>(cancellationToken: ct);
        if (payload is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON payload.", ct, "Validation");

        var result = await _service.HandleProviderNotificationsAsync(payload, ct);
        if (!result.IsSuccess)
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);

        return await req.OkJsonAsync(result.Value!, ct);
    }
}
