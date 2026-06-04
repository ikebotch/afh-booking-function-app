using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Self-Service Bookings")]
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
        "Self-Service Bookings",
        "View booking by secure client token",
        Description = "Client-facing booking details endpoint. Frontends must call this self-service route for client journeys, not the internal/admin booking details route. Provide the opaque client access token as the `token` query value. Invalid or expired tokens return 401. A valid token for a different booking returns 403. The response includes the current self-service links: `viewBookingUrl`, `cancelBookingUrl`, and `rescheduleBookingUrl`.",
        ResponseType = typeof(BookingDetailsResponse),
        ResponseExampleJson = """
                              {
                                "data": {
                                  "bookingId": "booking-123",
                                  "slotId": "slot-456",
                                  "transactionId": "transaction-789",
                                  "transactionRef": "TRX-10001",
                                  "adviserId": "adviser-1",
                                  "adviserName": "Adviser One",
                                  "startUtc": "2026-06-01T09:00:00Z",
                                  "endUtc": "2026-06-01T10:00:00Z",
                                  "durationMinutes": 60,
                                  "isRemote": true,
                                  "meetingType": "Remote",
                                  "status": "Confirmed",
                                  "confirmedUtc": "2026-05-26T10:15:00Z",
                                  "cancelledUtc": null,
                                  "cancelReason": null,
                                  "viewBookingUrl": "https://client.example.com/bookings/booking-123?token=opaque-client-token",
                                  "cancelBookingUrl": "https://client.example.com/bookings/booking-123/cancel?token=opaque-client-token",
                                  "rescheduleBookingUrl": "https://client.example.com/bookings/booking-123/reschedule?token=opaque-client-token"
                                }
                              }
                              """)]
    [BookingOpenApiQueryParameter("token", "string", Description = "Opaque client access token from the secure self-service link. Use this query parameter for the client self-service journey.", Example = "opaque-client-token")]
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
