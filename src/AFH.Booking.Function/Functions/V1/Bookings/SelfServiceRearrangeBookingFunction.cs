using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Self-Service Bookings")]
public sealed class SelfServiceRearrangeBookingFunction
{
    private readonly IBookingChangeAccessService _accessService;
    private readonly IRearrangeBookingService _service;

    public SelfServiceRearrangeBookingFunction(
        IBookingChangeAccessService accessService,
        IRearrangeBookingService service)
    {
        _accessService = accessService;
        _service = service;
    }

    [Function("Bookings_SelfServiceRearrange")]
    [BookingOpenApiOperation(
        "Self-Service Bookings",
        "Rearrange booking by secure client token",
        Description = "Client-facing rearrange endpoint. Frontends must call this self-service route for client journeys, not internal/admin rearrange routes. Provide the opaque client access token as the `token` query value; `accessToken` is also accepted as an alias. Invalid or expired tokens return 401. A valid token for a different booking returns 403. Rearranging creates a replacement booking; the old token must not be reused for the new booking.",
        RequestBodyType = typeof(RearrangeBookingRequest),
        ResponseType = typeof(RearrangeBookingResponse),
        RequestExampleJson = """
                             {
                               "newSlotId": "slot-new",
                               "reasonCode": "CLIENT_RESCHEDULE",
                               "reasonDetail": "Client selected a new time"
                             }
                             """,
        ResponseExampleJson = """
                              {
                                "data": {
                                  "previousBookingId": "booking-123",
                                  "newBookingId": "booking-456",
                                  "newSlotId": "slot-new",
                                  "previousAdviserId": "adviser-1",
                                  "previousAdviserName": "Adviser One",
                                  "previousStartUtc": "2026-06-01T09:00:00Z",
                                  "previousEndUtc": "2026-06-01T10:00:00Z",
                                  "newAdviserId": "adviser-1",
                                  "newAdviserName": "Adviser One",
                                  "newStartUtc": "2026-06-02T09:00:00Z",
                                  "newEndUtc": "2026-06-02T10:00:00Z",
                                  "notificationSummary": "Booking rearranged"
                                }
                              }
                              """)]
    [BookingOpenApiQueryParameter("token", "string", Description = "Opaque client access token from the secure self-service link. Use this query parameter for the client self-service journey.", Example = "opaque-client-token")]
    [BookingOpenApiQueryParameter("accessToken", "string", Description = "Alias for `token`. Supported for clients that already use this query name.", Example = "opaque-client-token")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/self-service/bookings/{bookingId}/rearrange")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var access = await BookingChangeRequestContext.ValidateClientAsync(req, bookingId, _accessService, ct);
        if (!access.IsSuccess || access.Value is null)
            return await req.ProblemAsync(access.StatusCode, access.ErrorMessage ?? "Unauthorized.", ct, access.ErrorCode);

        var body = await req.ReadJsonAsync<RearrangeBookingRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, Errors.Validation);

        var result = await _service.HandleAsync(new RearrangeBookingCommand
        {
            BookingId = bookingId.Trim(),
            NewSlotId = body.NewSlotId,
            RequestedBy = LifecycleActors.Client,
            ActorId = access.Value.ActorId,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail,
            CorrelationId = BookingChangeRequestContext.GetCorrelationId(req) ?? access.Value.CorrelationId
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
