using AFH.Booking.Function.Http;
using AFH.Booking.Function.Functions.V1.Notifications.Docs;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;

namespace AFH.Booking.Function.Functions.V1.Notifications.Inbound;

[BookingOpenApiTag("Notifications")]
public sealed class SubmitNotificationRequestFunction
{
    private readonly INotificationRequestIngestionService _ingestionService;

    public SubmitNotificationRequestFunction(INotificationRequestIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [Function("Notifications_RequestHttpV1")]
    [BookingOpenApiOperation(
        "Notifications",
        "Submit notification request",
        Description = "Accepts a notification request for asynchronous delivery. `data` is a flexible string map at runtime; the documented schema/example shows the complete booking notification payload keys currently emitted by the booking lifecycle. Client-only links such as manageBookingLink, manageBookingLinks, viewBookingUrl, cancelBookingUrl and rescheduleBookingUrl are stripped from adviser/contact-centre recipient copies.",
        RequestBodyType = typeof(BookingNotificationSubmitRequestExample),
        ResponseType = typeof(NotificationRequestAcceptedResponse),
        SuccessStatusCode = HttpStatusCode.Accepted,
        RequestExampleJson = """
        {
          "type": {
            "sourceApplication": "Booking",
            "name": "BookingConfirmed"
          },
          "correlationId": "booking-123-confirmed",
            "actor": {
            "actorType": "System",
            "sourceApplication": "Booking",
            "id": null,
            "displayName": null,
            "email": null
          },
          "recipients": [
            {
              "recipientType": "Client",
              "displayName": "Jane Client",
              "email": "jane.client@example.test",
              "mobileNumber": "+447700900123",
              "pushTarget": null,
              "preferredChannels": [ "Email" ]
            }
          ],
          "data": {
            "eventId": "booking-123-confirmed",
            "bookingId": "booking-123",
            "holdId": "booking-123",
            "slotId": "slot-123",
            "adviserId": "adv-001",
            "adviserName": "John Doe",
            "transactionRef": "TRX-123",
            "startUtc": "2026-07-01T09:00:00Z",
            "endUtc": "2026-07-01T10:00:00Z",
            "clientName": "Jane Client",
            "clientEmail": "jane.client@example.test",
            "clientPhone": "+447700900123",
            "meetingType": "Review",
            "meetingTopic": "Review",
            "meetingDate": "Wed 01 Jul 2026",
            "meetingDateDay": "Wed 01 Jul 2026",
            "meetingDateTime": "09:00-10:00 (Europe/London)",
            "date": "Wed 01 Jul 2026",
            "time": "09:00-10:00 (Europe/London)",
            "meetingMethod": "Online",
            "meetingDuration": "60 minutes",
            "meetingStatus": "Confirmed",
            "meetingAddress": "42 King Street, Manchester, M2 4LQ",
            "meetingAddressLine1": "42 King Street",
            "meetingAddressLine2": "",
            "meetingTown": "Manchester",
            "meetingCounty": "",
            "meetingPostcode": "M2 4LQ",
            "when": "Wed 01 Jul 2026 09:00 (Europe/London) to Wed 01 Jul 2026 10:00 (Europe/London)",
            "whenLine": "Wed 01 Jul 2026 09:00 (Europe/London) to Wed 01 Jul 2026 10:00 (Europe/London)",
            "whereLine": "Join link: https://meet.example.test/booking-123",
            "locationLine": "Online",
            "travelLine": "Travel: N/A (remote meeting)",
            "joinUrl": "https://meet.example.test/booking-123",
            "joinMeetingLink": "https://meet.example.test/booking-123",
            "manageBookingLink": "https://portal.example.test/bookings/booking-123",
            "manageBookingLinks": "Manage your booking:\n- View booking: https://portal.example.test/bookings/booking-123",
            "viewBookingUrl": "https://portal.example.test/bookings/booking-123",
            "cancelBookingUrl": "https://portal.example.test/bookings/booking-123/cancel",
            "rescheduleBookingUrl": "https://portal.example.test/bookings/booking-123/reschedule",
            "contactNumber": "0800 000 0000",
            "contactUsNumber": "0800 000 0000",
            "recipientType": "Client"
          }
        }
        """,
        ResponseExampleJson = """
        {
          "data": {
            "notificationRequestId": "11111111-1111-1111-1111-111111111111",
            "status": "Pending",
            "correlationId": "booking-123-confirmed"
          }
        }
        """)]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/requests")]
        HttpRequestData req,
        CancellationToken ct)
    {
        SubmitNotificationRequestDto? inboundRequest;
        try
        {
            inboundRequest = await req.ReadJsonAsync<SubmitNotificationRequestDto>(ct);
        }
        catch (JsonException)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body must be valid notification request JSON.", ct, "Validation");
        }

        if (inboundRequest is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Notification request body is required.", ct, "Validation");

        try
        {
            var request = inboundRequest.ToNotificationRequested();
            var result = await _ingestionService.AcceptAsync(request, ct);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new NotificationRequestAcceptedResponse(
                result.NotificationRequestId,
                result.Status,
                result.CorrelationId), cancellationToken: ct);
            return response;
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    public sealed record NotificationRequestAcceptedResponse(
        Guid NotificationRequestId,
        string Status,
        string CorrelationId);
}
