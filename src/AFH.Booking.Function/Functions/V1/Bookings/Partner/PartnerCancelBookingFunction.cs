using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class PartnerCancelBookingFunction
{
    private readonly ICancelBookingService _service;

    public PartnerCancelBookingFunction(ICancelBookingService service)
    {
        _service = service;
    }

    [Function("Bookings_PartnerCancel")]
    [BookingOpenApiOperation(
        "Bookings",
        "Cancel booking as partner",
        Description = "Partner cancellation endpoint. The partnerName route value is persisted into lifecycle audit as PartnerName while the lifecycle actor remains Partner.",
        RequestBodyType = typeof(CancelBookingRequest),
        ResponseType = typeof(CancelBookingResponse),
        RequestExampleJson = """
        {
          "reasonCode": "PARTNER_REQUEST",
          "reasonDetail": "Partner requested cancellation."
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/partners/{partnerName}/bookings/{bookingId}/cancel")]
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
        var body = await req.ReadJsonAsync<CancelBookingRequest>(ct) ?? new CancelBookingRequest();
        var correlationId = BookingChangeRequestContext.GetCorrelationId(req);
        var result = await _service.HandleAsync(new CancelBookingCommand
        {
            BookingId = bookingId.Trim(),
            ActorContext = BookingActorContext.Partner(partnerName, correlationId: correlationId),
            RequestedBy = LifecycleActors.Partner,
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
