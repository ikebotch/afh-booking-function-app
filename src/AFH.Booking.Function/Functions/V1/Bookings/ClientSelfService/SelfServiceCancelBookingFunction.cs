using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Self-Service Bookings")]
public sealed class SelfServiceCancelBookingFunction
{
    private readonly IBookingChangeAccessService _accessService;
    private readonly ICancelBookingService _service;

    public SelfServiceCancelBookingFunction(
        IBookingChangeAccessService accessService,
        ICancelBookingService service)
    {
        _accessService = accessService;
        _service = service;
    }

    [Function("Bookings_SelfServiceCancel")]
    [BookingOpenApiOperation(
        "Self-Service Bookings",
        "Cancel booking by secure client token",
        Description = "Client-facing cancellation endpoint. Frontends must call this self-service route for client journeys, not internal/admin cancellation routes. Provide the opaque client access token as the `token` query value. Invalid or expired tokens return 401. A valid token for a different booking returns 403.",
        RequestBodyType = typeof(CancelBookingRequest),
        RequestBodyRequired = false,
        ResponseType = typeof(CancelBookingResponse),
        RequestExampleJson = """
                             {
                               "reasonCode": "CLIENT_REQUEST",
                               "reason": "No longer needed"
                             }
                             """,
        ResponseExampleJson = """
                              {
                                "data": {
                                  "bookingId": "booking-123",
                                  "cancelledUtc": "2026-05-26T10:30:00Z",
                                  "status": "Cancelled"
                                }
                              }
                              """)]
    [BookingOpenApiQueryParameter("token", "string", Description = "Opaque client access token from the secure self-service link. Use this query parameter for the client self-service journey.", Example = "opaque-client-token")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/self-service/bookings/{bookingId}/cancel")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var access = await BookingChangeRequestContext.ValidateClientAsync(req, bookingId, _accessService, ct);
        if (!access.IsSuccess || access.Value is null)
            return await req.ProblemAsync(access.StatusCode, access.ErrorMessage ?? "Unauthorized.", ct, access.ErrorCode);

        var body = await req.ReadJsonAsync<CancelBookingRequest>(ct) ?? new CancelBookingRequest();
        var correlationId = BookingChangeRequestContext.GetCorrelationId(req) ?? access.Value.CorrelationId;
        var result = await _service.HandleAsync(new CancelBookingCommand
        {
            BookingId = bookingId.Trim(),
            ActorContext = BookingActorContext.SelfServiceClient(
                access.Value.ActorId,
                correlationId),
            RequestedBy = LifecycleActors.Client,
            ActorId = access.Value.ActorId,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail,
            Reason = body.Reason,
            CorrelationId = correlationId
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
