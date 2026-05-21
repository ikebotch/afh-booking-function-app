using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class LeadTechCancelBookingFunction
{
    private readonly ICancelBookingService _service;

    public LeadTechCancelBookingFunction(ICancelBookingService service)
    {
        _service = service;
    }

    [Function("Bookings_LeadTechCancel")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/leadtech/bookings/{bookingId}/cancel")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<CancelBookingRequest>(ct) ?? new CancelBookingRequest();
        var result = await _service.HandleAsync(new CancelBookingCommand
        {
            BookingId = bookingId.Trim(),
            RequestedBy = LifecycleActors.LeadTech,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail,
            Reason = body.Reason,
            CorrelationId = BookingChangeRequestContext.GetCorrelationId(req)
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
