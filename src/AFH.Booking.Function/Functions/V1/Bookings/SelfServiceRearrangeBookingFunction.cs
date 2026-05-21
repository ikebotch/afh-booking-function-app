using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
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
