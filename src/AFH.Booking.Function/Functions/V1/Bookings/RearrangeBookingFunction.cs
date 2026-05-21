using AFH.Booking.Application.Abstractions.Approvals;
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
    private readonly IApprovalWorkflowService _approvals;

    public RearrangeBookingFunction(
        IRearrangeBookingService service,
        IApprovalWorkflowService approvals)
    {
        _service = service;
        _approvals = approvals;
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

        var requestedBy = string.IsNullOrWhiteSpace(body.RequestedBy) ? "Client" : body.RequestedBy.Trim();

        if (string.Equals(requestedBy, "Adviser", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(body.ApprovalRequestId))
            {
                return await req.ProblemAsync(
                    HttpStatusCode.Forbidden,
                    "Adviser rearrangement requires an approved approvalRequestId.",
                    ct,
                    "ApprovalRequired");
            }

            var approved = await _approvals.IsApprovedAsync(
                body.ApprovalRequestId.Trim(),
                bookingId.Trim(),
                changeType: "Rearrange",
                requestedBy: "Adviser",
                ct: ct);

            if (!approved)
            {
                return await req.ProblemAsync(
                    HttpStatusCode.Forbidden,
                    "Approval request is not approved for this booking rearrangement.",
                    ct,
                    "ApprovalRequired");
            }
        }

        var cmd = new RearrangeBookingCommand
        {
            BookingId = bookingId.Trim(),
            NewSlotId = body.NewSlotId,
            RequestedBy = requestedBy,
            ReasonCode = body.ReasonCode,
            ReasonDetail = body.ReasonDetail,
            CorrelationId = req.Headers.TryGetValues("x-correlation-id", out var values) ? values.FirstOrDefault() : null
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
