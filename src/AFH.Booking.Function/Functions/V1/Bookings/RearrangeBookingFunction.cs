using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class RearrangeBookingFunction
{
    private readonly IRearrangeBookingService _service;

    public RearrangeBookingFunction(IRearrangeBookingService service)
    {
        _service = service;
    }

    [Function("Bookings_Rearrange")]
    [BookingOpenApiOperation(
        "Bookings",
        "Rearrange booking",
        RequestBodyType = typeof(RearrangeBookingRequest),
        ResponseType = typeof(RearrangeBookingResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/rearrange")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<RearrangeBookingRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        var requestedBy = string.IsNullOrWhiteSpace(body.RequestedBy)
            ? BookingActorContext.ActorInternalAdmin
            : body.RequestedBy.Trim();
        var correlationId = req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault()
            : null;

        var cmd = new RearrangeBookingCommand
        {
            BookingId = bookingId.Trim(),
            NewSlotId = body.NewSlotId,
            ActorContext = BookingActorContext.InternalAdmin(
                correlationId: correlationId,
                actorType: requestedBy),
            RequestedBy = requestedBy,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail,
            ApprovalRequestId = body.ApprovalRequestId,
            CorrelationId = correlationId
        };

        var result = await _service.HandleAsync(cmd, ct);

        if (!result.IsSuccess)
        {
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);
        }

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
