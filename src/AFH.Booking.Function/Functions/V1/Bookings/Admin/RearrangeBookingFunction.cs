using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class RearrangeBookingFunction
{
    private readonly IRearrangeBookingService _service;
    private readonly IBookingDetailsService _details;

    public RearrangeBookingFunction(
        IRearrangeBookingService service,
        IBookingDetailsService details)
    {
        _service = service;
        _details = details;
    }

    [Function("Bookings_Rearrange")]
    [BookingOpenApiOperation(
        "Bookings",
        "Rearrange booking",
        Description = "Manager/admin direct rearrangement endpoint. Requires an authenticated domain user with direct rearrangement permission. The route bookingId is the current existing booking. newSlotId and reasonCode are required. approvalRequestId is optional and links execution back to an approved adviser request when applicable.",
        RequestBodyType = typeof(RearrangeBookingRequest),
        ResponseType = typeof(RearrangeBookingResponse),
        RequestExampleJson = """
        {
          "newSlotId": "slot-456",
          "reasonCode": "ManagerApprovedRearrangement",
          "reasonDetail": "Manager approved adviser proposed slot.",
          "approvalRequestId": "approval-123"
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bookings/{bookingId}/rearrange")]
        HttpRequestData req,
        FunctionContext context,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<RearrangeBookingRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        var authResult = await BookingFunctionActorContext.BuildManagerOrAdminAsync(
            req,
            context,
            BookingPermissionNames.RearrangeDirect,
            ct);
        if (!authResult.IsSuccess)
            return authResult.Response!;

        var details = await _details.HandleAsync(new GetBookingDetailsQuery { BookingId = bookingId.Trim() }, ct);
        if (!details.IsSuccess)
            return await req.ProblemAsync(
                details.StatusCode,
                details.ErrorMessage ?? "Request failed.",
                ct,
                details.ErrorCode);

        var forbidden = await BookingFunctionActorContext.EnsureCanAccessBookingAsync(req, context.GetDomainUserContext()!, details.Value!, ct);
        if (forbidden is not null)
            return forbidden;

        if (string.IsNullOrWhiteSpace(body.NewSlotId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "newSlotId is required.", ct, Errors.Validation);

        if (string.IsNullOrWhiteSpace(body.ReasonCode))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "reasonCode is required for manager/admin booking rearrangement.", ct, Errors.ReasonCodeRequired);

        var actor = authResult.ActorContext!;

        var cmd = new RearrangeBookingCommand
        {
            BookingId = bookingId.Trim(),
            NewSlotId = body.NewSlotId,
            ActorContext = actor,
            RequestedBy = actor.ActorType,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail,
            ApprovalRequestId = body.ApprovalRequestId,
            CorrelationId = actor.CorrelationId
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
