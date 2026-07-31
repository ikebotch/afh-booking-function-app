using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Notifications")]
public sealed class SendBookingNotificationFunction
{
    private readonly IBookingNotificationRequestService _notifications;

    public SendBookingNotificationFunction(IBookingNotificationRequestService notifications)
    {
        _notifications = notifications;
    }

    [Function("Bookings_SendNotification")]
    [BookingOpenApiOperation(
        "Notifications",
        "Send booking notification",
        Description = "Queues a booking lifecycle notification for an existing booking. Supported eventType values are Booked, BookingConfirmed, Rearranged, BookingRescheduled, Cancelled, BookingCancelled, HoldCreated and BookingHoldCreated. The notification payload is rebuilt from persisted booking, slot, transaction and client data, including the standard booking notification variables shown on the generic notification request endpoint.",
        RequestBodyType = typeof(BookingNotificationRequest),
        ResponseType = typeof(NotificationDispatchResponse),
        RequestExampleJson = """
        {
          "eventType": "BookingConfirmed",
          "sendSms": false,
          "sendEmail": true,
          "messageOverride": null
        }
        """,
        ResponseExampleJson = """
        {
          "data": {
            "dispatchId": "manual-booking-123-11111111111111111111111111111111",
            "bookingId": "booking-123",
            "eventType": "BookingConfirmed",
            "smsRequested": false,
            "emailRequested": true,
            "smsStatus": "Skipped",
            "emailStatus": "Queued",
            "providerMessageId": null,
            "createdUtc": "2026-07-01T09:00:00Z"
          }
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/notifications/send")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<BookingNotificationRequest>(ct);
        var eventType = string.IsNullOrWhiteSpace(body?.EventType) ? string.Empty : body!.EventType.Trim();

        var response = await _notifications.SendAsync(
            bookingId: bookingId.Trim(),
            eventType: eventType,
            messageOverride: body?.MessageOverride,
            sendSms: body?.SendSms ?? true,
            sendEmail: body?.SendEmail ?? true,
            correlationId: GetCorrelationId(req),
            ct: ct);

        if (!response.IsSuccess || response.Value is null)
            return await req.ProblemAsync(response.StatusCode, response.ErrorMessage ?? "Request failed.", ct, response.ErrorCode);

        return await req.OkJsonAsync(response.Value.ToContract(), ct);
    }

    private static string? GetCorrelationId(HttpRequestData req)
        => req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
            : null;
}
