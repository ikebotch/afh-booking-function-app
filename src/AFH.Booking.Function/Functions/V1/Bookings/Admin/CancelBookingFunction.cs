using System.Net;
using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class CancelBookingFunction
{
    private readonly ICancelBookingService _service;
    private readonly ILogger<CancelBookingFunction> _logger;

    public CancelBookingFunction(
        ICancelBookingService service,
        ILogger<CancelBookingFunction> logger)
    {
        _service = service;
        _logger = logger;
    }

    [Function("Bookings_CancelBooking")]
    [BookingOpenApiOperation(
        "Bookings",
        "Cancel booking",
        RequestBodyType = typeof(CancelBookingRequest),
        ResponseType = typeof(CancelBookingResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bookings/{bookingId}/cancel")]
        HttpRequestData req,
        FunctionContext context,
        string bookingId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

            var body = await req.ReadJsonAsync<CancelBookingRequest>(ct);
            var authResult = await BookingFunctionActorContext.BuildManagerOrAdminAsync(
                req,
                context,
                BookingPermissionNames.CancelDirect,
                ct);
            if (!authResult.IsSuccess)
                return authResult.Response!;

            if (string.IsNullOrWhiteSpace(body?.ReasonCode))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "reasonCode is required for manager/admin booking cancellation.", ct, Errors.ReasonCodeRequired);

            var actor = authResult.ActorContext!;

            var cmd = new CancelBookingCommand
            {
                BookingId = bookingId.Trim(),
                ActorContext = actor,
                Reason = BuildReason(body, actor.ActorType),
                RequestedBy = actor.ActorType,
                ReasonCode = body?.ReasonCode,
                ReasonDetail = body?.ReasonDetail,
                ApprovalRequestId = body?.ApprovalRequestId,
                CorrelationId = actor.CorrelationId
            };

            var result = await _service.HandleAsync(cmd, ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(
                    result.StatusCode,
                    result.ErrorMessage ?? "Request failed.",
                    ct,
                    result.ErrorCode);

            // If your service returns a payload (recommended)
            if (result.Value is not null)
                return await req.OkJsonAsync(result.Value!.ToContract(), ct);

            // If service returns success without data
            return await req.OkJsonAsync(new { message = "Booking cancelled." }, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON body in Bookings_CancelBooking.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct, "InvalidJson");
        }
    }

    private static string BuildReason(CancelBookingRequest? request, string actorType)
    {
        if (request is null)
            return "Cancelled";

        if (!string.IsNullOrWhiteSpace(request.Reason))
            return request.Reason.Trim();

        var reasonCode = string.IsNullOrWhiteSpace(request.ReasonCode)
            ? "Unspecified"
            : request.ReasonCode.Trim();

        var detail = string.IsNullOrWhiteSpace(request.ReasonDetail)
            ? string.Empty
            : $": {request.ReasonDetail.Trim()}";

        return $"{actorType} - {reasonCode}{detail}";
    }
}
