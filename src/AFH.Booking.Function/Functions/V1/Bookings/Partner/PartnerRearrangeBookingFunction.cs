using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class PartnerRearrangeBookingFunction
{
    private readonly IRearrangeBookingService _service;

    public PartnerRearrangeBookingFunction(IRearrangeBookingService service)
    {
        _service = service;
    }

    [Function("Bookings_PartnerRearrange")]
    [BookingOpenApiOperation(
        "Bookings",
        "Rearrange booking as partner",
        Description = "Partner rearrangement endpoint. The partnerName route value is persisted into lifecycle audit as PartnerName while the lifecycle actor remains Partner.",
        RequestBodyType = typeof(RearrangeBookingRequest),
        ResponseType = typeof(RearrangeBookingResponse),
        RequestExampleJson = """
        {
          "newSlotId": "slot-456",
          "reasonCode": "PARTNER_RESCHEDULE",
          "reasonDetail": "Partner requested a new appointment slot."
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/partners/{partnerName}/bookings/{bookingId}/rearrange")]
        HttpRequestData req,
        string partnerName,
        string bookingId,
        CancellationToken ct)
        => await HandleAsync(req, partnerName, bookingId, ct);

    private async Task<HttpResponseData> HandleAsync(
        HttpRequestData req,
        string partnerName,
        string bookingId,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<RearrangeBookingRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, Errors.Validation);

        var correlationId = BookingChangeRequestContext.GetCorrelationId(req);
        var result = await _service.HandleAsync(new RearrangeBookingCommand
        {
            BookingId = bookingId.Trim(),
            NewSlotId = body.NewSlotId,
            ActorContext = BookingActorContext.Partner(partnerName, correlationId: correlationId),
            RequestedBy = LifecycleActors.Partner,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail,
            CorrelationId = correlationId
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
