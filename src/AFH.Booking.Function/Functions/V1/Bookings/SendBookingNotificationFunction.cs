using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Notifications")]
public sealed class SendBookingNotificationFunction
{
    private readonly IClientNotificationService _notifications;

    public SendBookingNotificationFunction(IClientNotificationService notifications)
    {
        _notifications = notifications;
    }

    [Function("Bookings_SendNotification")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/notifications/send")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<BookingNotificationRequest>(ct);
        var eventType = string.IsNullOrWhiteSpace(body?.EventType) ? "BookingChanged" : body!.EventType.Trim();

        var response = await _notifications.SendBookingNotificationAsync(
            bookingId: bookingId.Trim(),
            eventType: eventType,
            message: body?.MessageOverride,
            sendSms: body?.SendSms ?? true,
            sendEmail: body?.SendEmail ?? true,
            ct: ct);

        return await req.OkJsonAsync(response.ToContract(), ct);
    }
}
